using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TourismHcm.Application.Abstractions.Providers;
using TourismHcm.Domain.Constants;
using TourismHcm.Domain.Entities;
using TourismHcm.Domain.Enums;
using TourismHcm.Domain.Helpers;
using TourismHcm.Infrastructure.Persistence;

namespace TourismHcm.Infrastructure.Seed;

/// <summary>
/// Seeds baseline reference data (roles, an administrator account, and a starter catalogue of
/// well-known Ho Chi Minh City tourist attractions) so a freshly provisioned Development
/// database is immediately usable. Intended to be invoked once at application startup, only
/// outside of Production.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(
        TourismHcmDbContext dbContext,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(dbContext, ct);
        await SeedAdminUserAsync(dbContext, configuration, passwordHasher, logger, ct);
        await SeedPlacesAsync(dbContext, ct);
        // Offline-friendly: Manual English guides so multilingual demo works without Google API keys.
        // Japanese/Korean/Chinese requests then fall back to English (isFallback=true) and Web Speech speaks en.
        await SeedMissingEnglishTranslationsAsync(dbContext, logger, ct);

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(TourismHcmDbContext dbContext, CancellationToken ct)
    {
        foreach (var roleName in AppRoles.All)
        {
            var exists = await dbContext.Roles.AnyAsync(r => r.Name == roleName, ct);
            if (!exists)
            {
                dbContext.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    PermissionsJson = JsonSerializer.Serialize(AppPermissions.DefaultPermissionsForRole(roleName)),
                    Priority = RolePriorityFor(roleName),
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);

        var leftoverOwner = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "place_owner", ct);
        if (leftoverOwner is not null)
        {
            var links = dbContext.Set<UserRole>().Where(ur => ur.RoleId == leftoverOwner.Id);
            dbContext.RemoveRange(links);
            dbContext.Roles.Remove(leftoverOwner);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private static int RolePriorityFor(string roleName) => roleName switch
    {
        AppRoles.SuperAdmin => 0,
        AppRoles.Admin => 1,
        AppRoles.EditorLegacyName => 2,
        AppRoles.User => 100,
        _ => 1000,
    };

    private static async Task SeedAdminUserAsync(
        TourismHcmDbContext dbContext,
        IConfiguration configuration,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken ct)
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Admin:Email / Admin:Password are not configured; skipping administrator account seeding.");
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existing = await dbContext.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // Development: keep password in sync with Admin:Password so appsettings/.env changes apply
        // after the first seed (otherwise login fails with a stale hash).
        if (existing is not null)
        {
            if (!passwordHasher.Verify(password, existing.PasswordHash))
            {
                existing.PasswordHash = passwordHasher.Hash(password);
                existing.IsActive = true;
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation("Updated Development admin password hash for {Email}.", normalizedEmail);
            }

            return;
        }

        var adminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == AppRoles.Admin, ct);
        if (adminRole is null)
        {
            logger.LogWarning("Admin role not found; cannot seed administrator account.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = "Administrator",
            PasswordHash = passwordHasher.Hash(password),
            IsActive = true,
            CreatedAt = now,
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });

        var superAdminRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == AppRoles.SuperAdmin, ct);
        if (superAdminRole is not null)
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = superAdminRole.Id });
        }

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation("Seeded Development admin account {Email}.", normalizedEmail);
    }

    private static async Task SeedPlacesAsync(TourismHcmDbContext dbContext, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var definitions = BuildPlaceDefinitions();

        var alreadySeeded = await dbContext.Places.AnyAsync(ct);
        if (!alreadySeeded)
        {
            foreach (var definition in definitions)
            {
                var place = definition.ToPlace(now);
                dbContext.Places.Add(place);

                // Mirror the Vietnamese source description as an explicit "vi" translation row so
                // every supported language-including the source language-shows up uniformly in
                // admin tooling that lists Place.Translations.
                dbContext.PlaceTranslations.Add(new PlaceTranslation
                {
                    Id = Guid.NewGuid(),
                    PlaceId = place.Id,
                    Lang = AppLanguages.Vietnamese,
                    Text = place.DescriptionVi,
                    Source = TranslationSource.Manual,
                    ContentHash = place.ContentHash,
                    UpdatedAt = now,
                });
            }

            await dbContext.SaveChangesAsync(ct);
            return;
        }

        // Refresh seed catalogue facts (ratings, address, hours, tickets, images, coords, ward, copy).
        var byName = definitions.ToDictionary(d => d.Name, StringComparer.Ordinal);
        var names = byName.Keys.ToList();
        var existing = await dbContext.Places.Where(p => names.Contains(p.Name)).ToListAsync(ct);
        var dirty = false;
        foreach (var place in existing)
        {
            if (!byName.TryGetValue(place.Name, out var definition))
            {
                continue;
            }
            var changed =
                Math.Abs(place.AverageRating - definition.Rating) >= 0.001
                || place.ReviewCount != definition.ReviewCount
                || place.Address != definition.Address
                || place.OpeningHours != definition.OpeningHours
                || place.TicketPrice != definition.TicketPrice
                || place.ImageUrl != definition.ImageUrl
                || place.District != definition.District
                || place.DescriptionVi != definition.DescriptionVi
                || Math.Abs(place.Latitude - definition.Latitude) >= 0.00001
                || Math.Abs(place.Longitude - definition.Longitude) >= 0.00001;

            if (!changed)
            {
                continue;
            }

            place.AverageRating = definition.Rating;
            place.ReviewCount = definition.ReviewCount;
            place.Address = definition.Address;
            place.OpeningHours = definition.OpeningHours;
            place.TicketPrice = definition.TicketPrice;
            place.ImageUrl = definition.ImageUrl;
            place.District = definition.District;
            place.DescriptionVi = definition.DescriptionVi;
            place.ContentHash = ContentHash.Compute(definition.DescriptionVi);
            place.Latitude = definition.Latitude;
            place.Longitude = definition.Longitude;
            place.UpdatedAt = now;
            dirty = true;
        }

        if (dirty)
        {
            var placeIds = existing.Select(p => p.Id).ToList();
            var viRows = await dbContext.PlaceTranslations
                .Where(t => t.Lang == AppLanguages.Vietnamese && placeIds.Contains(t.PlaceId))
                .ToListAsync(ct);
            var byId = existing.ToDictionary(p => p.Id);
            foreach (var row in viRows)
            {
                if (!byId.TryGetValue(row.PlaceId, out var place) || row.Text == place.DescriptionVi)
                {
                    continue;
                }

                row.Text = place.DescriptionVi;
                row.ContentHash = place.ContentHash;
                row.UpdatedAt = now;
            }

            await dbContext.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Adds Manual English narration for places that only have Vietnamese yet.
    /// Enables no-cloud demos: choose ja/ko/zh → content falls back to English → browser Web Speech reads it.
    /// </summary>
    private static async Task SeedMissingEnglishTranslationsAsync(
        TourismHcmDbContext dbContext,
        ILogger logger,
        CancellationToken ct)
    {
        var places = await dbContext.Places.AsNoTracking().ToListAsync(ct);
        if (places.Count == 0)
        {
            return;
        }

        var existingEn = await dbContext.PlaceTranslations
            .Where(t => t.Lang == AppLanguages.English)
            .ToListAsync(ct);
        var haveEn = existingEn.ToDictionary(t => t.PlaceId);

        var now = DateTimeOffset.UtcNow;
        var added = 0;
        var updated = 0;

        foreach (var place in places)
        {
            if (!EnglishGuides.TryGetValue(place.Name, out var guide))
            {
                if (haveEn.ContainsKey(place.Id))
                {
                    continue;
                }

                guide =
                    $"Welcome to {place.Name}. This English guide is sample offline content for demos without cloud translation. {place.DescriptionVi}";
            }

            if (haveEn.TryGetValue(place.Id, out var row))
            {
                if (row.Text == guide)
                {
                    continue;
                }

                row.Text = guide;
                row.ContentHash = place.ContentHash;
                row.UpdatedAt = now;
                updated++;
                continue;
            }

            dbContext.PlaceTranslations.Add(new PlaceTranslation
            {
                Id = Guid.NewGuid(),
                PlaceId = place.Id,
                Lang = AppLanguages.English,
                Text = guide,
                Source = TranslationSource.Manual,
                ContentHash = place.ContentHash,
                UpdatedAt = now,
            });
            added++;
        }

        if (added > 0 || updated > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation(
                "Seeded {Added} and refreshed {Updated} Manual English place translations for offline/no-API-key demos.",
                added,
                updated);
        }
    }

    /// <summary>Manual English tour texts (demo quality) keyed by Vietnamese place name.</summary>
    private static readonly Dictionary<string, string> EnglishGuides = new(StringComparer.Ordinal)
    {
        ["Dinh Độc Lập"] =
            "Independence Palace, also known as Reunification Palace, is one of Saigon’s most important historic landmarks. Once the workplace of the President of the Republic of Vietnam, it witnessed the historic moment at noon on 30 April 1975 when a liberation tank crashed through the main gate, marking national reunification. The modern building blends Eastern philosophy with wide gardens, a state banquet hall, a command bunker and secret tunnels. Visitors can tour presidential offices, a cinema room, the rooftop helipad and wartime exhibits that bring a pivotal chapter of Vietnamese history to life.",
        ["Nhà thờ Đức Bà"] =
            "Notre-Dame Cathedral Basilica of Saigon is an architectural icon built by the French between 1877 and 1880 in a Romanesque style with Gothic touches. Materials from cement to roof tiles were shipped from France. Unusually, the church uses exposed red bricks without plaster, helping it shed water and resist humidity for more than a century. Twin bell towers over 60 metres high frame the city centre, while the statue of Our Lady of Peace in the forecourt is a favourite photo stop for visitors exploring Ward Bến Nghé.",
        ["Bưu điện Trung tâm Sài Gòn"] =
            "Saigon Central Post Office stands beside Notre-Dame Cathedral and was built from 1886 to 1891 in classical French style. Its symmetrical façade, arched windows and vaulted interior still hold an antique map of Saigon and Cochinchina and a century-old wall clock. Beyond postal services it is a beloved sightseeing and souvenir stop, drawing international visitors who come for its rare colonial atmosphere amid the modern city.",
        ["Chợ Bến Thành"] =
            "Ben Thanh Market is one of Saigon’s oldest icons, dating from 1912, with its famous clock tower facing Quach Thi Trang roundabout. Four gates open to the four cardinal directions, and inside thousands of stalls sell fabrics, clothes, handicrafts, dry goods and southern street food. It is ideal for soaking up the bustle, tasting bánh xèo or hủ tiếu, and picking up souvenirs; the nearby night market keeps food and shopping going late into the evening.",
        ["Bảo tàng Chứng tích Chiến tranh"] =
            "The War Remnants Museum is among Vietnam’s most visited museums by international travellers. Thousands of artefacts, documents and photographs document the human cost of conflict, especially the American War—Agent Orange consequences, a Con Dao tiger-cage reconstruction, outdoor tanks and aircraft, and powerful press images. It offers a sobering, often emotional perspective on peace and modern Vietnamese history.",
        ["Phố đi bộ Nguyễn Huệ"] =
            "Nguyen Hue Walking Street is the city’s grand central boulevard, running from City Hall to the Saigon River at Bach Dang. Evenings and weekends fill with street culture, light shows, music and major outdoor events, framed by skyscrapers, hotels and cafés. It is also home to the famous Nguyen Hue flower street each Lunar New Year, when locals and tourists crowd the avenue for photos and celebration.",
        ["Landmark 81"] =
            "Landmark 81 is Vietnam’s tallest tower at 81 storeys, rising beside the Saigon River in Vinhomes Central Park. Inspired by bundles of bamboo reaching skyward, it houses residences, offices, Vincom mall and the Landmark 81 SkyView observatory with 360-degree glass views over the city and river. At night LED lighting makes it a defining piece of Saigon’s skyline and a symbol of the city’s rapid growth.",
        ["Thảo Cầm Viên Sài Gòn"] =
            "Saigon Zoo and Botanical Gardens, founded in 1864, is among the world’s oldest zoos and Vietnam’s oldest. Vast grounds with century-old trees hold hundreds of species—tigers, elephants, giraffes, primates, birds and reptiles—plus rich botanical collections. Families and students love this green escape in the city centre; the Ho Chi Minh City Museum of History on site lets you combine nature with culture in one visit.",
        ["Chợ Lớn (Chợ Bình Tây)"] =
            "Cholon, centred on Binh Tay Market, is the historic heart of Saigon’s Chinese community, blending Hoa–Viet culture, architecture and food. Built in the 1920s by a Chinese merchant, Binh Tay features curved roofs, a central watchtower and a wide courtyard, serving as a major wholesale hub for fabrics, household goods, spices and traditional medicine. Nearby guild halls and temples such as Thien Hau Pagoda reward a walking tour of this distinctive neighbourhood.",
        ["Địa đạo Củ Chi"] =
            "The Cu Chi Tunnels are a world-famous underground defence network dug during the wars against France and later expanded against the United States—over 250 kilometres of passages with meeting rooms, clinics, kitchens and workshops. Visitors can crawl a short authentic section, learn about guerrilla traps, watch historical films and taste cassava with sesame salt, the staple of wartime life—an unforgettable lesson in Vietnamese resilience.",
        ["Rừng ngập mặn Cần Giờ"] =
            "Can Gio Mangrove Forest is a UNESCO Biosphere Reserve about 50 kilometres southeast of central Ho Chi Minh City. Restored after wartime damage, it shelters monkeys on Monkey Island, waterbirds, crocodiles and classic tidal flora such as mắm and đước trees. Boat trips through shady canals, the historic Rung Sac base and fresh local seafood make it a perfect nature day-trip away from urban noise.",
        ["Bảo tàng Mỹ thuật TP. Hồ Chí Minh"] =
            "The Fine Arts Museum occupies a striking French–Chinese villa once owned by a wealthy Chinese merchant family. Carved wooden stairs, ornate ironwork and clever tropical ventilation frame thousands of paintings, sculptures, ceramics and folk art from ancient to modern Vietnam. Beautiful natural light also makes it a favourite photography spot for younger visitors alongside its serious artistic value.",
        ["Chùa Ngọc Hoàng"] =
            "Ngoc Hoang Pagoda, or Phuoc Hai Tu, is one of Saigon’s most distinctive Chinese temples, built in the early twentieth century to honour the Jade Emperor. Traditional roofs, intricate wood and paper craft, and incense-filled halls create a solemn atmosphere. Famous for prayers for children and romance—and visited by President Barack Obama—it also has a turtle pond where locals release turtles for blessings of peace and health.",
        ["Nhà hát Thành phố"] =
            "The Municipal Theatre, or Saigon Opera House, was built from 1898 in flamboyant French colonial style inspired by European opera houses, with an ornate sculpted façade. Sitting between the Continental Hotel and Nguyen Hue Walking Street, it hosts water puppetry, folk dance, symphony, cải lương and tourist shows such as À Ố Show. Illuminated at night, it is one of the most photographed landmarks in Ward Bến Nghé.",
        ["Bitexco Financial Tower"] =
            "Bitexco Financial Tower is an iconic Saigon skyscraper shaped like a lotus bud, Vietnam’s national flower, with a dramatic helipad jutting out above 190 metres. The 68-storey tower holds offices, the Icon68 mall and Saigon Skydeck on floor 49 for sweeping views of the river and skyline. After dark its lights help define the city’s modern financial-district horizon.",
    };

    private static List<PlaceSeedDefinition> BuildPlaceDefinitions()
    {
        var definitions = new List<PlaceSeedDefinition>
        {
            new(
                "Dinh Độc Lập",
                10.77694444, 106.69527778,
                PlaceCategory.Historical,
                "Bến Thành",
                "135 Nam Kỳ Khởi Nghĩa, Phường Bến Thành, TP. Hồ Chí Minh (cổng phụ: 106 Nguyễn Du)",
                "Dinh Độc Lập, còn gọi là Dinh Thống Nhất, là một trong những công trình lịch sử quan trọng nhất của Sài Gòn - TP. Hồ Chí Minh. Nơi đây từng là nơi làm việc của Tổng thống Việt Nam Cộng hòa và là nơi chứng kiến thời khắc lịch sử trưa ngày 30 tháng 4 năm 1975, khi xe tăng quân Giải phóng húc đổ cổng chính, đánh dấu ngày đất nước thống nhất. Công trình mang kiến trúc hiện đại kết hợp triết lý phương Đông, với sân vườn rộng, phòng khánh tiết, hầm chỉ huy tác chiến và hệ thống đường hào bí mật. Du khách có thể tham quan phòng làm việc của Tổng thống, phòng chiếu phim, sân thượng có bãi đáp trực thăng và khu trưng bày hiện vật chiến tranh, qua đó hiểu thêm về một giai đoạn lịch sử hào hùng của dân tộc Việt Nam.",
                50,
                "07:00 – 18:00 hàng ngày (kể cả lễ, Tết; trừ thông báo đặc biệt)",
                "Vé toàn bộ (Dinh + Nhà trưng bày): 80.000 VNĐ (18–59); 40.000 VNĐ (60+/SV VN); 20.000 VNĐ (6–17). Vé giới hạn: 40.000 / 20.000 / 10.000 VNĐ. Xe điện: 25.000 VNĐ/lượt (áp dụng từ 01/01/2026).",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7d/20190923_Independence_Palace-10.jpg/1280px-20190923_Independence_Palace-10.jpg",
                "lich-su,dinh-doc-lap,di-tich,kien-truc",
                4.4, 32774),
            new(
                "Nhà thờ Đức Bà",
                10.77972222, 106.69916667,
                PlaceCategory.Religious,
                "Bến Nghé",
                "01 Công trường Công xã Paris, Phường Bến Nghé, TP. Hồ Chí Minh",
                "Nhà thờ Đức Bà Sài Gòn là một trong những biểu tượng kiến trúc nổi bật nhất của thành phố, được người Pháp xây dựng từ năm 1877 đến 1880 theo phong cách Roman cải biên pha trộn nét Gothic. Toàn bộ vật liệu xây dựng, từ xi măng, sắt thép đến ngói lợp đều được chuyển từ Pháp sang. Điểm đặc biệt là nhà thờ không có bàn thờ bằng đá như thông thường mà được xây hoàn toàn bằng gạch trần không trát, có khả năng thoát nước và chống ẩm tốt dù trải qua hơn một thế kỷ. Hai tháp chuông cao hơn 60 mét với bộ chuông đồng lớn do Pháp chế tạo khiến công trình trở nên uy nghi giữa trung tâm thành phố. Phía trước nhà thờ là tượng Đức Mẹ Hòa Bình, địa điểm chụp ảnh được yêu thích của du khách trong và ngoài nước khi ghé thăm khu vực trung tâm thành phố.",
                40,
                "Tham quan bên ngoài tự do; nội thất đang trùng tu dài hạn, hạn chế vào bên trong theo thông báo Giáo phận",
                "Miễn phí tham quan bên ngoài / quảng trường",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d7/Bas%C3%ADlica_de_Nuestra_Se%C3%B1ora%2C_Ciudad_Ho_Chi_Minh%2C_Vietnam%2C_2013-08-14%2C_DD_03.JPG/1280px-Bas%C3%ADlica_de_Nuestra_Se%C3%B1ora%2C_Ciudad_Ho_Chi_Minh%2C_Vietnam%2C_2013-08-14%2C_DD_03.JPG",
                "nha-tho,kien-truc-phap,ton-giao,bieu-tuong",
                4.4, 28702),
            new(
                "Bưu điện Trung tâm Sài Gòn",
                10.78, 106.7,
                PlaceCategory.Landmark,
                "Bến Nghé",
                "02 Công trường Công xã Paris, Phường Bến Nghé, TP. Hồ Chí Minh",
                "Bưu điện Trung tâm Sài Gòn nằm ngay cạnh Nhà thờ Đức Bà, được xây dựng từ năm 1886 đến 1891 theo thiết kế mang đậm dấu ấn kiến trúc Pháp cổ điển kết hợp phong cách châu Âu thế kỷ 19. Công trình nổi bật với mặt tiền đối xứng, các ô cửa vòm lớn và nội thất mái vòm cong tráng lệ, bên trong còn lưu giữ tấm bản đồ Sài Gòn và Nam Kỳ cổ xưa cùng chiếc đồng hồ lớn gắn trên tường đã hoạt động suốt hơn một trăm năm. Đây không chỉ là nơi phục vụ các dịch vụ bưu chính, viễn thông truyền thống mà còn là điểm tham quan, chụp ảnh và mua quà lưu niệm nổi tiếng, thu hút rất nhiều du khách quốc tế mỗi ngày vì vẻ đẹp hoài cổ hiếm có giữa lòng đô thị hiện đại.",
                30,
                "T2–T6: 07:00 – 19:00; T7: 07:00 – 18:00; CN: 08:00 – 18:00 (theo VisithCMC)",
                "Miễn phí tham quan; dịch vụ bưu chính theo bảng giá VNPost",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/03/Saigon_Central_Post_Office_%2852681461470%29.jpg/1280px-Saigon_Central_Post_Office_%2852681461470%29.jpg",
                "kien-truc-phap,bieu-tuong,buu-dien",
                4.4, 5773),
            new(
                "Chợ Bến Thành",
                10.77252069, 106.69801918,
                PlaceCategory.Market,
                "Bến Thành",
                "Công trường Quách Thị Trang (cổng chính), Phường Bến Thành, TP. Hồ Chí Minh",
                "Chợ Bến Thành là một trong những biểu tượng lâu đời và nổi tiếng nhất của Sài Gòn, được xây dựng từ năm 1912 với kiến trúc đặc trưng gồm tháp đồng hồ ở cổng chính hướng ra vòng xoay Quách Thị Trang. Chợ có bốn cổng chính tương ứng với bốn hướng Đông - Tây - Nam - Bắc, bên trong là hàng ngàn gian hàng bày bán đa dạng mặt hàng từ vải vóc, quần áo, giày dép, đồ thủ công mỹ nghệ đến đặc sản, thực phẩm khô và ẩm thực đường phố đặc trưng miền Nam. Đây là nơi lý tưởng để du khách trải nghiệm không khí mua bán nhộn nhịp, thử các món ăn truyền thống như bánh xèo, hủ tiếu, chè và mua quà lưu niệm mang về. Khu vực xung quanh chợ về đêm còn có chợ đêm Bến Thành phục vụ ẩm thực và mua sắm cho tới khuya.",
                60,
                "Chợ chính: 07:00 – 19:00 hàng ngày; chợ đêm: 19:00 – 24:00",
                "Miễn phí vào chợ; giá hàng hóa thương lượng",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f5/Ben_Thanh%2C_Ciudad_Ho_Chi_Minh%2C_Vietnam%2C_2013-08-14%2C_DD_01.JPG/1280px-Ben_Thanh%2C_Ciudad_Ho_Chi_Minh%2C_Vietnam%2C_2013-08-14%2C_DD_01.JPG",
                "cho,mua-sam,am-thuc,bieu-tuong",
                4.0, 65189),
            new(
                "Bảo tàng Chứng tích Chiến tranh",
                10.779475, 106.692132,
                PlaceCategory.Museum,
                "Xuân Hòa",
                "28 Võ Văn Tần, Phường Xuân Hòa, TP. Hồ Chí Minh",
                "Bảo tàng Chứng tích Chiến tranh là một trong những bảo tàng thu hút nhiều du khách quốc tế nhất tại Việt Nam, lưu giữ và trưng bày hàng nghìn hiện vật, tư liệu, hình ảnh phản ánh chân thực hậu quả của các cuộc chiến tranh mà Việt Nam đã trải qua, đặc biệt là chiến tranh chống Mỹ. Các chuyên đề nổi bật gồm hậu quả của chất độc da cam/dioxin, chuồng cọp Côn Đảo mô phỏng, bộ sưu tập vũ khí, xe tăng, máy bay chiến đấu được trưng bày ngoài trời và những bức ảnh báo chí nổi tiếng của các nhà báo chiến trường. Bảo tàng mang đến góc nhìn sâu sắc, đôi khi gây xúc động mạnh về giá trị của hòa bình, phù hợp cho những ai muốn tìm hiểu lịch sử Việt Nam hiện đại một cách trực quan và chân thực.",
                40,
                "07:30 – 17:30 hàng ngày (quầy vé đến 17:00)",
                "40.000 VNĐ/người lớn; giảm/miễn theo đối tượng ưu tiên (trẻ em dưới 6 miễn phí). Nguồn: baotangchungtichchientranh.vn",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/War_Remnants_Museum%2C_HCMC%2C_front.JPG/1280px-War_Remnants_Museum%2C_HCMC%2C_front.JPG",
                "bao-tang,lich-su,chien-tranh",
                4.5, 40536),
            new(
                "Phố đi bộ Nguyễn Huệ",
                10.7735, 106.7032,
                PlaceCategory.Entertainment,
                "Bến Nghé",
                "Đường Nguyễn Huệ, từ UBND TP đến bến Bạch Đằng, Phường Bến Nghé, TP. Hồ Chí Minh",
                "Phố đi bộ Nguyễn Huệ là trục đại lộ trung tâm sầm uất nhất của TP. Hồ Chí Minh, kéo dài từ trước UBND Thành phố đến bến Bạch Đằng ven sông Sài Gòn. Vào các buổi tối, đặc biệt là cuối tuần và ngày lễ, nơi đây trở thành không gian sinh hoạt công cộng nhộn nhịp với nhiều hoạt động văn hóa, nghệ thuật đường phố, trình diễn ánh sáng, nhạc nước và các sự kiện ngoài trời quy mô lớn. Hai bên phố là những tòa nhà cao tầng hiện đại, khách sạn sang trọng, trung tâm thương mại và quán cà phê, tạo nên khung cảnh vừa hiện đại vừa gần gũi. Đây cũng là nơi diễn ra lễ hội đường hoa Nguyễn Huệ vào mỗi dịp Tết Nguyên Đán, thu hút đông đảo người dân và du khách đến tham quan, chụp ảnh lưu niệm.",
                80,
                "Mở cửa tự do 24/7; sôi động nhất khoảng 18:00 – 22:00",
                "Miễn phí",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/75/Nguyen_Hue_Street_2020.jpg/1280px-Nguyen_Hue_Street_2020.jpg",
                "pho-di-bo,giai-tri,trung-tam,ve-dem",
                4.6, 18245),
            new(
                "Landmark 81",
                10.795, 106.722,
                PlaceCategory.Landmark,
                "Thạnh Mỹ Tây",
                "720A Điện Biên Phủ, Phường Thạnh Mỹ Tây (trước đây P.22), TP. Hồ Chí Minh — Vincom Center Landmark 81",
                "Landmark 81 là tòa tháp cao nhất Việt Nam với 81 tầng, thuộc khu đô thị Vinhomes Central Park ven sông Sài Gòn, lấy cảm hứng thiết kế từ hình ảnh những bó tre truyền thống của Việt Nam vươn lên trời cao. Tòa tháp hội tụ căn hộ cao cấp, văn phòng, trung tâm thương mại Vincom và đặc biệt là đài quan sát Landmark 81 SkyView ở các tầng trên cao, nơi du khách có thể phóng tầm mắt bao quát toàn cảnh thành phố, dòng sông Sài Gòn và khu đô thị hiện đại từ trên cao qua hệ thống kính cường lực toàn cảnh 360 độ. Vào buổi tối, công trình được chiếu đèn led đổi màu rực rỡ, trở thành điểm nhấn không thể thiếu của đường chân trời Sài Gòn và là một trong những biểu tượng mới cho sự phát triển vượt bậc của thành phố.",
                100,
                "Đài quan sát SkyView: 10:00 – 21:30 hàng ngày (Vincom)",
                "SkyView T2–T6: 300.000 VNĐ (>1,4 m) / 150.000 VNĐ (<1,4 m). T7–CN & lễ: 500.000 / 250.000 VNĐ. Nguồn: vincom.com.vn",
                "https://upload.wikimedia.org/wikipedia/commons/1/14/Landmark_81_in_2021.jpg",
                "toa-nha-cao-tang,bieu-tuong,view-thanh-pho",
                4.6, 23989),
            new(
                "Thảo Cầm Viên Sài Gòn",
                10.78805556, 106.70472222,
                PlaceCategory.Park,
                "Bến Nghé",
                "02 Nguyễn Bỉnh Khiêm (cổng A), Phường Bến Nghé, TP. Hồ Chí Minh",
                "Thảo Cầm Viên Sài Gòn, được xây dựng từ năm 1864, là một trong những vườn thú lâu đời nhất trên thế giới và lâu đời nhất tại Việt Nam. Khuôn viên rộng lớn với nhiều cây cổ thụ hàng trăm năm tuổi, quy tụ hàng trăm loài động vật quý hiếm như hổ, voi, hươu cao cổ, khỉ, các loài chim và bò sát, cùng khu vườn thực vật đa dạng gồm hoa lan, cây cảnh và các loài cây bản địa. Đây là điểm đến yêu thích của các gia đình có trẻ nhỏ, học sinh và những ai muốn tìm một không gian xanh mát để dạo bộ, nghỉ ngơi giữa lòng thành phố ồn ào. Bên trong khuôn viên còn có Bảo tàng Lịch sử TP. Hồ Chí Minh, giúp du khách kết hợp tham quan thiên nhiên với tìm hiểu văn hóa, lịch sử trong cùng một chuyến đi.",
                50,
                "07:00 – 18:00 hàng ngày (ngừng bán vé 17:30)",
                "Cao từ 1,3 m: 60.000 VNĐ; 1,0–1,3 m: 40.000 VNĐ; dưới 1,0 m (đi cùng phụ huynh): miễn phí. Nguồn: saigonzoo.vn",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3d/Thao_cam_vien_%2C_Phuong_Da_kao%2C_quan_1%2C_tphcmvn_-_panoramio.jpg/1280px-Thao_cam_vien_%2C_Phuong_Da_kao%2C_quan_1%2C_tphcmvn_-_panoramio.jpg",
                "cong-vien,vuon-thu,gia-dinh,thien-nhien",
                4.3, 36610),
            new(
                "Chợ Lớn (Chợ Bình Tây)",
                10.749375, 106.651078,
                PlaceCategory.Market,
                "Bình Tây",
                "57A Tháp Mười, Phường Bình Tây, TP. Hồ Chí Minh",
                "Chợ Lớn, với trung tâm là chợ Bình Tây, nằm giữa khu vực đông đảo cộng đồng người Hoa sinh sống lâu đời tại TP. Hồ Chí Minh, mang đậm dấu ấn văn hóa, kiến trúc và ẩm thực Hoa - Việt giao thoa. Chợ Bình Tây được một thương gia người Hoa xây dựng từ những năm 1920, nổi bật với kiến trúc cổ kiểu Trung Hoa, mái ngói cong, tháp canh ở trung tâm và khoảng sân trong rộng rãi. Đây là đầu mối bán sỉ lớn với hàng vạn mặt hàng từ vải vóc, đồ gia dụng đến thực phẩm khô, gia vị, các loại trà và thuốc Bắc truyền thống. Dạo quanh khu Chợ Lớn, du khách còn có thể ghé thăm các hội quán, chùa người Hoa cổ kính như Hội quán Tuệ Thành, thưởng thức ẩm thực đường phố đặc trưng Hoa kiều rất phong phú.",
                60,
                "Khoảng 06:00 – 18:00 hàng ngày (sầm uất buổi sáng)",
                "Miễn phí vào chợ; giá hàng hóa thương lượng",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bf/Vn-hcm-cho-binh-tay-27-07-07.JPG/1280px-Vn-hcm-cho-binh-tay-27-07-07.JPG",
                "cho,van-hoa-hoa,mua-sam,am-thuc",
                4.4, 6406),
            new(
                "Địa đạo Củ Chi",
                11.06083333, 106.52861111,
                PlaceCategory.Historical,
                "Phú Mỹ Hưng",
                "Khu di tích địa đạo Củ Chi (Bến Đình / Bến Dược), Phường Phú Mỹ Hưng, TP. Hồ Chí Minh",
                "Địa đạo Củ Chi là hệ thống đường hầm phòng thủ nổi tiếng thế giới, được quân và dân Củ Chi đào từ thời kháng chiến chống Pháp và được mở rộng mạnh mẽ trong kháng chiến chống Mỹ, với tổng chiều dài lên tới hơn 250 km, bao gồm nhiều tầng, phòng họp, bệnh xá, bếp ăn, hầm chứa vũ khí nằm sâu dưới lòng đất. Đến đây, du khách sẽ được trải nghiệm chui qua một đoạn địa đạo thực tế, tìm hiểu các loại hầm chông, bẫy tự tạo tinh vi của du kích, xem phim tư liệu lịch sử và thưởng thức món khoai mì chấm muối vừng - món ăn gắn liền với đời sống kháng chiến gian khổ. Đây là điểm đến giáo dục lịch sử sâu sắc, giúp thế hệ trẻ và du khách quốc tế hiểu rõ hơn về ý chí kiên cường của người Việt Nam.",
                100,
                "07:00 – 17:00 hàng ngày",
                "Khách quốc tế (từ 01/02/2026): phí tham quan 35.000 VNĐ + vé phục vụ/hướng dẫn 100.000 VNĐ. Khách nội địa theo bảng giá tại quầy. Nguồn: diadaocuchi.com.vn",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/14/C%E1%BB%A7_Chi_tunnels_entrance.JPG/1280px-C%E1%BB%A7_Chi_tunnels_entrance.JPG",
                "lich-su,chien-tranh,di-tich,trai-nghiem",
                4.5, 14581),
            new(
                "Rừng ngập mặn Cần Giờ",
                10.4084, 106.9485,
                PlaceCategory.Nature,
                "Cần Thạnh",
                "Khu du lịch sinh thái / Đảo Khỉ — Khu dự trữ sinh quyển Cần Giờ, Phường Cần Thạnh, TP. Hồ Chí Minh",
                "Rừng ngập mặn Cần Giờ là Khu Dự trữ Sinh quyển Thế giới được UNESCO công nhận, nằm cách trung tâm TP. Hồ Chí Minh khoảng 50 km về phía Đông Nam, nơi hệ sinh thái rừng ngập mặn ven biển phục hồi và phát triển mạnh mẽ sau chiến tranh. Khu vực này là nơi trú ngụ của hàng trăm loài động, thực vật đặc trưng vùng ngập nước lợ, nổi bật nhất là đảo Khỉ với hàng ngàn cá thể khỉ đuôi dài sinh sống bán hoang dã, cùng các loài chim nước, cá sấu, kỳ đà. Du khách có thể trải nghiệm đi thuyền len lỏi qua các kênh rạch rợp bóng cây mắm, cây đước, tham quan căn cứ Rừng Sác lịch sử, thưởng thức hải sản tươi sống địa phương. Đây là điểm đến lý tưởng cho những ai yêu thích thiên nhiên, muốn tìm một chuyến đi trong ngày tránh xa sự ồn ào đô thị.",
                150,
                "Khu tham quan phổ biến khoảng 07:00 – 17:00 (tùy điểm trong khu dự trữ)",
                "Vé vào các điểm sinh thái (Đảo Khỉ / Rừng Sác) theo bảng giá tại cổng — thường khoảng vài chục nghìn VNĐ/người; thuê thuyền riêng",
                "https://upload.wikimedia.org/wikipedia/commons/e/e5/Mangrove_in_Can_Gio_forest.jpg",
                "thien-nhien,rung-ngap-man,sinh-thai,dao-khi",
                4.3, 3102),
            new(
                "Bảo tàng Mỹ thuật TP. Hồ Chí Minh",
                10.76944444, 106.7,
                PlaceCategory.Museum,
                "Nguyễn Thái Bình",
                "97A Phó Đức Chính, Phường Nguyễn Thái Bình, TP. Hồ Chí Minh",
                "Bảo tàng Mỹ thuật TP. Hồ Chí Minh được đặt trong một tòa biệt thự cổ theo kiến trúc Pháp - Hoa pha trộn độc đáo, từng là dinh thự của một gia đình thương gia người Hoa giàu có bậc nhất Sài Gòn xưa. Công trình nổi bật với hệ thống cầu thang gỗ chạm khắc tinh xảo, cửa sắt uốn hoa văn nhập từ Pháp, hệ thống thông gió và lấy sáng thông minh phù hợp khí hậu nhiệt đới. Bên trong bảo tàng trưng bày hàng nghìn tác phẩm hội họa, điêu khắc, gốm sứ và nghệ thuật dân gian Việt Nam qua nhiều thời kỳ, từ cổ đại đến hiện đại, bao gồm nhiều tác phẩm của các danh họa nổi tiếng. Không gian kiến trúc độc đáo cùng ánh sáng tự nhiên tuyệt đẹp khiến nơi đây trở thành điểm chụp ảnh yêu thích của giới trẻ bên cạnh giá trị nghệ thuật, lịch sử sâu sắc.",
                30,
                "08:00 – 17:00 hàng ngày (kiểm tra thông báo đóng cửa từng nhà trưng bày)",
                "Khoảng 40.000 VNĐ/người lớn (theo bảng giá tại quầy; ưu tiên học sinh/sinh viên)",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0f/B%E1%BA%A3o_t%C3%A0ng_M%E1%BB%B9_thu%E1%BA%ADt_Tp_%28ki%E1%BA%BFn_tr%C3%BAc_t%E1%BB%95ng_th%E1%BB%83%29_%282%29.jpg/1280px-B%E1%BA%A3o_t%C3%A0ng_M%E1%BB%B9_thu%E1%BA%ADt_Tp_%28ki%E1%BA%BFn_tr%C3%BAc_t%E1%BB%95ng_th%E1%BB%83%29_%282%29.jpg",
                "bao-tang,nghe-thuat,kien-truc-co",
                4.5, 1144),
            new(
                "Chùa Ngọc Hoàng",
                10.79171111, 106.69788889,
                PlaceCategory.Religious,
                "Đa Kao",
                "73 Mai Thị Lựu, Phường Đa Kao, TP. Hồ Chí Minh",
                "Chùa Ngọc Hoàng, còn gọi là Phước Hải Tự, là một trong những ngôi chùa cổ linh thiêng và đặc sắc nhất của người Hoa tại Sài Gòn, được xây dựng vào đầu thế kỷ 20 để thờ Ngọc Hoàng Thượng Đế theo tín ngưỡng dân gian Trung Hoa. Kiến trúc chùa mang đậm phong cách Hoa truyền thống với mái ngói âm dương, các hoa văn chạm khắc tinh xảo bằng gỗ và giấy bồi, cùng không gian khói hương trầm mặc tạo cảm giác huyền bí, cổ kính. Chùa nổi tiếng với tín ngưỡng cầu con, cầu duyên và từng được Tổng thống Mỹ Barack Obama ghé thăm trong một chuyến công tác, góp phần đưa địa danh này đến gần hơn với du khách quốc tế. Khuôn viên chùa còn có hồ nuôi rùa và cá, là nơi người dân thường đến thả rùa cầu bình an, sức khỏe.",
                40,
                "Khoảng 07:00 – 18:00 hàng ngày (giờ thắp hương có thể kéo dài)",
                "Miễn phí tham quan / công đức tùy tâm",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/1/10/Jade_Emperor_Pagoda_Saigon.jpg/1280px-Jade_Emperor_Pagoda_Saigon.jpg",
                "chua,ton-giao,tin-nguong,kien-truc-hoa",
                4.7, 2042),
            new(
                "Nhà hát Thành phố",
                10.77655, 106.70315,
                PlaceCategory.Entertainment,
                "Bến Nghé",
                "07 Công trường Lam Sơn, Phường Bến Nghé, TP. Hồ Chí Minh",
                "Nhà hát Thành phố, hay Nhà hát Lớn Sài Gòn, được xây dựng từ năm 1898 theo phong cách kiến trúc flamboyant đặc trưng thời Pháp thuộc, lấy cảm hứng từ các nhà hát opera châu Âu cổ điển với mặt tiền trang trí tượng đắp nổi và các chi tiết điêu khắc công kỳ công. Nằm ngay giữa trung tâm thành phố, kế bên khách sạn Continental và phố đi bộ Nguyễn Huệ, nhà hát là nơi tổ chức thường xuyên các buổi biểu diễn nghệ thuật đỉnh cao như múa rối nước, xòe dân gian, nhạc giao hưởng, cải lương và các show trình diễn văn hóa Việt Nam dành cho du khách quốc tế như À Ố Show. Về đêm, công trình được chiếu sáng lộng lẫy, trở thành một trong những background chụp ảnh được yêu thích nhất khi dạo quanh khu vực trung tâm thành phố.",
                50,
                "Tham quan mặt ngoài tự do; lịch diễn theo show (thường buổi tối)",
                "Miễn phí xem mặt ngoài; vé biểu diễn (À Ố Show / ballet nhạc…) theo từng suất, thường từ vài trăm nghìn VNĐ",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7d/Ho_Chi_Minh_City_Opera_House_07-11-2024.jpg/1280px-Ho_Chi_Minh_City_Opera_House_07-11-2024.jpg",
                "nha-hat,kien-truc-phap,nghe-thuat,ve-dem",
                4.6, 17932),
            new(
                "Bitexco Financial Tower",
                10.77166667, 106.70444444,
                PlaceCategory.Landmark,
                "Bến Nghé",
                "02 Hải Triều, Phường Bến Nghé, TP. Hồ Chí Minh",
                "Bitexco Financial Tower là một trong những tòa tháp mang tính biểu tượng của TP. Hồ Chí Minh, nổi bật với thiết kế độc đáo lấy cảm hứng từ hình ảnh búp sen - loài hoa quốc gia của Việt Nam, cùng với sân đậu trực thăng vươn ra khỏi thân tòa nhà ở độ cao hơn 190 mét, một chi tiết kiến trúc hiếm thấy trên thế giới. Tòa tháp cao 68 tầng, là nơi đặt văn phòng của nhiều doanh nghiệp lớn, trung tâm thương mại cao cấp Icon68 và đặc biệt là đài quan sát Saigon Skydeck ở tầng 49, nơi du khách có thể ngắm toàn cảnh thành phố, dòng sông Sài Gòn uốn lượn và những tòa cao tầng lân cận từ trên cao. Vào ban đêm, tòa tháp rực sáng góp phần tạo nên đường chân trời hiện đại, lộng lẫy cho khu trung tâm tài chính của thành phố.",
                50,
                "Saigon Skydeck: 09:30 – 21:30 hàng ngày (vé cuối ~45 phút trước đóng cửa)",
                "Người lớn: 240.000 VNĐ; trẻ em 4–12 / người cao tuổi 65+ / NKT: 160.000 VNĐ; dưới 4 tuổi miễn phí (đi kèm NL). Nguồn: bitexcofinancialtower.com",
                "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c4/DJI_0550-HDR-Pano_Bitexco_Financial_Tower.jpg/1280px-DJI_0550-HDR-Pano_Bitexco_Financial_Tower.jpg",
                "toa-nha-cao-tang,bieu-tuong,view-thanh-pho",
                4.5, 19603),
        };

        return definitions;
    }

    /// <summary>
    /// Rating / ReviewCount approximate Google Maps figures for demo parity (not live API sync).
    /// </summary>
    private sealed record PlaceSeedDefinition(
        string Name,
        double Latitude,
        double Longitude,
        PlaceCategory Category,
        string District,
        string Address,
        string DescriptionVi,
        int TriggerRadiusMeters,
        string OpeningHours,
        string TicketPrice,
        string ImageUrl,
        string Tags,
        double Rating,
        int ReviewCount)
    {
        public Place ToPlace(DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            Name = Name,
            DescriptionVi = DescriptionVi,
            ContentHash = ContentHash.Compute(DescriptionVi),
            Category = Category,
            District = District,
            Address = Address,
            Latitude = Latitude,
            Longitude = Longitude,
            TriggerRadiusMeters = TriggerRadiusMeters,
            OpeningHours = OpeningHours,
            TicketPrice = TicketPrice,
            ImageUrl = ImageUrl,
            Tags = Tags,
            AverageRating = Rating,
            ReviewCount = ReviewCount,
            Status = PlaceStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
