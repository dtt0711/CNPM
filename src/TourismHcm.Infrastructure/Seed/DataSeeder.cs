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

        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(TourismHcmDbContext dbContext, CancellationToken ct)
    {
        foreach (var roleName in AppRoles.All)
        {
            var exists = await dbContext.Roles.AnyAsync(r => r.Name == roleName, ct);
            if (!exists)
            {
                dbContext.Roles.Add(new Role { Id = Guid.NewGuid(), Name = roleName });
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

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
        var alreadyExists = await dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (alreadyExists)
        {
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

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task SeedPlacesAsync(TourismHcmDbContext dbContext, CancellationToken ct)
    {
        var alreadySeeded = await dbContext.Places.AnyAsync(ct);
        if (alreadySeeded)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var places = BuildPlaces(now);

        foreach (var place in places)
        {
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
    }

    private static List<Place> BuildPlaces(DateTimeOffset now)
    {
        var definitions = new List<PlaceSeedDefinition>
        {
            new(
                "Dinh Độc Lập",
                // VERIFY coords
                10.7769, 106.6959,
                PlaceCategory.Historical,
                "Quận 1",
                "135 Nam Kỳ Khởi Nghĩa, Phường Bến Thành, Quận 1, TP. Hồ Chí Minh",
                "Dinh Độc Lập, còn gọi là Dinh Thống Nhất, là một trong những công trình lịch sử quan trọng nhất của Sài Gòn - TP. Hồ Chí Minh. Nơi đây từng là nơi làm việc của Tổng thống Việt Nam Cộng hòa và là nơi chứng kiến thời khắc lịch sử trưa ngày 30 tháng 4 năm 1975, khi xe tăng quân Giải phóng húc đổ cổng chính, đánh dấu ngày đất nước thống nhất. Công trình mang kiến trúc hiện đại kết hợp triết lý phương Đông, với sân vườn rộng, phòng khánh tiết, hầm chỉ huy tác chiến và hệ thống đường hào bí mật. Du khách có thể tham quan phòng làm việc của Tổng thống, phòng chiếu phim, sân thượng có bãi đáp trực thăng và khu trưng bày hiện vật chiến tranh, qua đó hiểu thêm về một giai đoạn lịch sử hào hùng của dân tộc Việt Nam.",
                50,
                "SAMPLE DATA: 07:30 - 11:00, 13:00 - 16:00 hàng ngày",
                "SAMPLE DATA: 65.000 VNĐ/người lớn, miễn phí cho trẻ em dưới 1m2",
                "https://picsum.photos/seed/dinh-doc-lap/800/600",
                "lich-su,dinh-doc-lap,di-tich,kien-truc",
                4.6),
            new(
                "Nhà thờ Đức Bà",
                // VERIFY coords
                10.7798, 106.6990,
                PlaceCategory.Religious,
                "Quận 1",
                "01 Công Xã Paris, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Nhà thờ Đức Bà Sài Gòn là một trong những biểu tượng kiến trúc nổi bật nhất của thành phố, được người Pháp xây dựng từ năm 1877 đến 1880 theo phong cách Roman cải biên pha trộn nét Gothic. Toàn bộ vật liệu xây dựng, từ xi măng, sắt thép đến ngói lợp đều được chuyển từ Pháp sang. Điểm đặc biệt là nhà thờ không có bàn thờ bằng đá như thông thường mà được xây hoàn toàn bằng gạch trần không trát, có khả năng thoát nước và chống ẩm tốt dù trải qua hơn một thế kỷ. Hai tháp chuông cao hơn 60 mét với bộ chuông đồng lớn do Pháp chế tạo khiến công trình trở nên uy nghi giữa trung tâm thành phố. Phía trước nhà thờ là tượng Đức Mẹ Hòa Bình, địa điểm chụp ảnh được yêu thích của du khách trong và ngoài nước khi ghé thăm khu vực trung tâm Quận 1.",
                40,
                "SAMPLE DATA: tham quan bên ngoài tự do; bên trong đang trùng tu, hạn chế giờ mở",
                "SAMPLE DATA: miễn phí tham quan bên ngoài",
                "https://picsum.photos/seed/nha-tho-duc-ba/800/600",
                "nha-tho,kien-truc-phap,ton-giao,bieu-tuong",
                4.7),
            new(
                "Bưu điện Trung tâm Sài Gòn",
                // VERIFY coords
                10.7798, 106.6999,
                PlaceCategory.Landmark,
                "Quận 1",
                "02 Công Xã Paris, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Bưu điện Trung tâm Sài Gòn nằm ngay cạnh Nhà thờ Đức Bà, được xây dựng từ năm 1886 đến 1891 theo thiết kế mang đậm dấu ấn kiến trúc Pháp cổ điển kết hợp phong cách châu Âu thế kỷ 19. Công trình nổi bật với mặt tiền đối xứng, các ô cửa vòm lớn và nội thất mái vòm cong tráng lệ, bên trong còn lưu giữ tấm bản đồ Sài Gòn và Nam Kỳ cổ xưa cùng chiếc đồng hồ lớn gắn trên tường đã hoạt động suốt hơn một trăm năm. Đây không chỉ là nơi phục vụ các dịch vụ bưu chính, viễn thông truyền thống mà còn là điểm tham quan, chụp ảnh và mua quà lưu niệm nổi tiếng, thu hút rất nhiều du khách quốc tế mỗi ngày vì vẻ đẹp hoài cổ hiếm có giữa lòng đô thị hiện đại.",
                30,
                "SAMPLE DATA: 07:00 - 19:00 hàng ngày",
                "SAMPLE DATA: miễn phí tham quan",
                "https://picsum.photos/seed/buu-dien-trung-tam/800/600",
                "kien-truc-phap,bieu-tuong,buu-dien",
                4.5),
            new(
                "Chợ Bến Thành",
                // VERIFY coords
                10.7720, 106.6983,
                PlaceCategory.Market,
                "Quận 1",
                "Đường Lê Lợi, Phường Bến Thành, Quận 1, TP. Hồ Chí Minh",
                "Chợ Bến Thành là một trong những biểu tượng lâu đời và nổi tiếng nhất của Sài Gòn, được xây dựng từ năm 1912 với kiến trúc đặc trưng gồm tháp đồng hồ ở cổng chính hướng ra vòng xoay Quách Thị Trang. Chợ có bốn cổng chính tương ứng với bốn hướng Đông - Tây - Nam - Bắc, bên trong là hàng ngàn gian hàng bày bán đa dạng mặt hàng từ vải vóc, quần áo, giày dép, đồ thủ công mỹ nghệ đến đặc sản, thực phẩm khô và ẩm thực đường phố đặc trưng miền Nam. Đây là nơi lý tưởng để du khách trải nghiệm không khí mua bán nhộn nhịp, thử các món ăn truyền thống như bánh xèo, hủ tiếu, chè và mua quà lưu niệm mang về. Khu vực xung quanh chợ về đêm còn có chợ đêm Bến Thành phục vụ ẩm thực và mua sắm cho tới khuya.",
                60,
                "SAMPLE DATA: 07:00 - 19:00 (chợ chính); chợ đêm 19:00 - 23:00",
                "SAMPLE DATA: miễn phí vào chợ, giá hàng hóa thương lượng",
                "https://picsum.photos/seed/cho-ben-thanh/800/600",
                "cho,mua-sam,am-thuc,bieu-tuong",
                4.3),
            new(
                "Bảo tàng Chứng tích Chiến tranh",
                // VERIFY coords
                10.7795, 106.6922,
                PlaceCategory.Museum,
                "Quận 3",
                "28 Võ Văn Tần, Phường Võ Thị Sáu, Quận 3, TP. Hồ Chí Minh",
                "Bảo tàng Chứng tích Chiến tranh là một trong những bảo tàng thu hút nhiều du khách quốc tế nhất tại Việt Nam, lưu giữ và trưng bày hàng nghìn hiện vật, tư liệu, hình ảnh phản ánh chân thực hậu quả của các cuộc chiến tranh mà Việt Nam đã trải qua, đặc biệt là chiến tranh chống Mỹ. Các chuyên đề nổi bật gồm hậu quả của chất độc da cam/dioxin, chuồng cọp Côn Đảo mô phỏng, bộ sưu tập vũ khí, xe tăng, máy bay chiến đấu được trưng bày ngoài trời và những bức ảnh báo chí nổi tiếng của các nhà báo chiến trường. Bảo tàng mang đến góc nhìn sâu sắc, đôi khi gây xúc động mạnh về giá trị của hòa bình, phù hợp cho những ai muốn tìm hiểu lịch sử Việt Nam hiện đại một cách trực quan và chân thực.",
                40,
                "SAMPLE DATA: 07:30 - 17:00 hàng ngày",
                "SAMPLE DATA: 40.000 VNĐ/người lớn",
                "https://picsum.photos/seed/chung-tich-chien-tranh/800/600",
                "bao-tang,lich-su,chien-tranh",
                4.5),
            new(
                "Phố đi bộ Nguyễn Huệ",
                // VERIFY coords
                10.7740, 106.7030,
                PlaceCategory.Entertainment,
                "Quận 1",
                "Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Phố đi bộ Nguyễn Huệ là trục đại lộ trung tâm sầm uất nhất của TP. Hồ Chí Minh, kéo dài từ trước UBND Thành phố đến bến Bạch Đằng ven sông Sài Gòn. Vào các buổi tối, đặc biệt là cuối tuần và ngày lễ, nơi đây trở thành không gian sinh hoạt công cộng nhộn nhịp với nhiều hoạt động văn hóa, nghệ thuật đường phố, trình diễn ánh sáng, nhạc nước và các sự kiện ngoài trời quy mô lớn. Hai bên phố là những tòa nhà cao tầng hiện đại, khách sạn sang trọng, trung tâm thương mại và quán cà phê, tạo nên khung cảnh vừa hiện đại vừa gần gũi. Đây cũng là nơi diễn ra lễ hội đường hoa Nguyễn Huệ vào mỗi dịp Tết Nguyên Đán, thu hút đông đảo người dân và du khách đến tham quan, chụp ảnh lưu niệm.",
                80,
                "SAMPLE DATA: mở tự do 24/7, hoạt động sôi động nhất 18:00 - 22:00",
                "SAMPLE DATA: miễn phí",
                "https://picsum.photos/seed/pho-di-bo-nguyen-hue/800/600",
                "pho-di-bo,giai-tri,trung-tam,ve-dem",
                4.6),
            new(
                "Landmark 81",
                // VERIFY coords
                10.7951, 106.7219,
                PlaceCategory.Landmark,
                "Bình Thạnh",
                "720A Điện Biên Phủ, Phường 22, Quận Bình Thạnh, TP. Hồ Chí Minh",
                "Landmark 81 là tòa tháp cao nhất Việt Nam với 81 tầng, thuộc khu đô thị Vinhomes Central Park ven sông Sài Gòn, lấy cảm hứng thiết kế từ hình ảnh những bó tre truyền thống của Việt Nam vươn lên trời cao. Tòa tháp hội tụ căn hộ cao cấp, văn phòng, trung tâm thương mại Vincom và đặc biệt là đài quan sát Landmark 81 SkyView ở các tầng trên cao, nơi du khách có thể phóng tầm mắt bao quát toàn cảnh thành phố, dòng sông Sài Gòn và khu đô thị hiện đại từ trên cao qua hệ thống kính cường lực toàn cảnh 360 độ. Vào buổi tối, công trình được chiếu đèn led đổi màu rực rỡ, trở thành điểm nhấn không thể thiếu của đường chân trời Sài Gòn và là một trong những biểu tượng mới cho sự phát triển vượt bậc của thành phố.",
                100,
                "SAMPLE DATA: đài quan sát mở 09:30 - 22:00 hàng ngày",
                "SAMPLE DATA: 310.000 VNĐ/người lớn (đài quan sát)",
                "https://picsum.photos/seed/landmark-81/800/600",
                "toa-nha-cao-tang,bieu-tuong,view-thanh-pho",
                4.7),
            new(
                "Thảo Cầm Viên Sài Gòn",
                // VERIFY coords
                10.7875, 106.7050,
                PlaceCategory.Park,
                "Quận 1",
                "02 Nguyễn Bỉnh Khiêm, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Thảo Cầm Viên Sài Gòn, được xây dựng từ năm 1864, là một trong những vườn thú lâu đời nhất trên thế giới và lâu đời nhất tại Việt Nam. Khuôn viên rộng lớn với nhiều cây cổ thụ hàng trăm năm tuổi, quy tụ hàng trăm loài động vật quý hiếm như hổ, voi, hươu cao cổ, khỉ, các loài chim và bò sát, cùng khu vườn thực vật đa dạng gồm hoa lan, cây cảnh và các loài cây bản địa. Đây là điểm đến yêu thích của các gia đình có trẻ nhỏ, học sinh và những ai muốn tìm một không gian xanh mát để dạo bộ, nghỉ ngơi giữa lòng thành phố ồn ào. Bên trong khuôn viên còn có Bảo tàng Lịch sử TP. Hồ Chí Minh, giúp du khách kết hợp tham quan thiên nhiên với tìm hiểu văn hóa, lịch sử trong cùng một chuyến đi.",
                50,
                "SAMPLE DATA: 07:00 - 18:00 hàng ngày",
                "SAMPLE DATA: 60.000 VNĐ/người lớn, 15.000 VNĐ/trẻ em",
                "https://picsum.photos/seed/thao-cam-vien/800/600",
                "cong-vien,vuon-thu,gia-dinh,thien-nhien",
                4.4),
            new(
                "Chợ Lớn (Chợ Bình Tây)",
                // VERIFY coords
                10.7498, 106.6511,
                PlaceCategory.Market,
                "Quận 6",
                "57A Tháp Mười, Phường 2, Quận 6, TP. Hồ Chí Minh",
                "Chợ Lớn, với trung tâm là chợ Bình Tây, nằm giữa khu vực đông đảo cộng đồng người Hoa sinh sống lâu đời tại TP. Hồ Chí Minh, mang đậm dấu ấn văn hóa, kiến trúc và ẩm thực Hoa - Việt giao thoa. Chợ Bình Tây được một thương gia người Hoa xây dựng từ những năm 1920, nổi bật với kiến trúc cổ kiểu Trung Hoa, mái ngói cong, tháp canh ở trung tâm và khoảng sân trong rộng rãi. Đây là đầu mối bán sỉ lớn với hàng vạn mặt hàng từ vải vóc, đồ gia dụng đến thực phẩm khô, gia vị, các loại trà và thuốc Bắc truyền thống. Dạo quanh khu Chợ Lớn, du khách còn có thể ghé thăm các hội quán, chùa người Hoa cổ kính như Hội quán Tuệ Thành, thưởng thức ẩm thực đường phố đặc trưng Hoa kiều rất phong phú.",
                60,
                "SAMPLE DATA: 06:00 - 18:00 hàng ngày",
                "SAMPLE DATA: miễn phí vào chợ, giá hàng hóa thương lượng",
                "https://picsum.photos/seed/cho-lon/800/600",
                "cho,van-hoa-hoa,mua-sam,am-thuc",
                4.2),
            new(
                "Địa đạo Củ Chi",
                // VERIFY coords
                11.1439, 106.4953,
                PlaceCategory.Historical,
                "Củ Chi",
                "Ấp Phú Hiệp, Xã Phú Mỹ Hưng, Huyện Củ Chi, TP. Hồ Chí Minh",
                "Địa đạo Củ Chi là hệ thống đường hầm phòng thủ nổi tiếng thế giới, được quân và dân Củ Chi đào từ thời kháng chiến chống Pháp và được mở rộng mạnh mẽ trong kháng chiến chống Mỹ, với tổng chiều dài lên tới hơn 250 km, bao gồm nhiều tầng, phòng họp, bệnh xá, bếp ăn, hầm chứa vũ khí nằm sâu dưới lòng đất. Đến đây, du khách sẽ được trải nghiệm chui qua một đoạn địa đạo thực tế, tìm hiểu các loại hầm chông, bẫy tự tạo tinh vi của du kích, xem phim tư liệu lịch sử và thưởng thức món khoai mì chấm muối vừng - món ăn gắn liền với đời sống kháng chiến gian khổ. Đây là điểm đến giáo dục lịch sử sâu sắc, giúp thế hệ trẻ và du khách quốc tế hiểu rõ hơn về ý chí kiên cường của người Việt Nam.",
                100,
                "SAMPLE DATA: 07:00 - 17:00 hàng ngày",
                "SAMPLE DATA: 110.000 VNĐ/người lớn (khách quốc tế)",
                "https://picsum.photos/seed/dia-dao-cu-chi/800/600",
                "lich-su,chien-tranh,di-tich,trai-nghiem",
                4.6),
            new(
                "Rừng ngập mặn Cần Giờ",
                // VERIFY coords
                10.4084, 106.9485,
                PlaceCategory.Nature,
                "Cần Giờ",
                "Khu Dự trữ Sinh quyển Rừng ngập mặn Cần Giờ, Huyện Cần Giờ, TP. Hồ Chí Minh",
                "Rừng ngập mặn Cần Giờ là Khu Dự trữ Sinh quyển Thế giới được UNESCO công nhận, nằm cách trung tâm TP. Hồ Chí Minh khoảng 50 km về phía Đông Nam, nơi hệ sinh thái rừng ngập mặn ven biển phục hồi và phát triển mạnh mẽ sau chiến tranh. Khu vực này là nơi trú ngụ của hàng trăm loài động, thực vật đặc trưng vùng ngập nước lợ, nổi bật nhất là đảo Khỉ với hàng ngàn cá thể khỉ đuôi dài sinh sống bán hoang dã, cùng các loài chim nước, cá sấu, kỳ đà. Du khách có thể trải nghiệm đi thuyền len lỏi qua các kênh rạch rợp bóng cây mắm, cây đước, tham quan căn cứ Rừng Sác lịch sử, thưởng thức hải sản tươi sống địa phương. Đây là điểm đến lý tưởng cho những ai yêu thích thiên nhiên, muốn tìm một chuyến đi trong ngày tránh xa sự ồn ào đô thị.",
                150,
                "SAMPLE DATA: 07:00 - 17:00 hàng ngày",
                "SAMPLE DATA: 50.000 VNĐ/người lớn (vé vào khu đảo Khỉ)",
                "https://picsum.photos/seed/can-gio/800/600",
                "thien-nhien,rung-ngap-man,sinh-thai,dao-khi",
                4.4),
            new(
                "Bảo tàng Mỹ thuật TP. Hồ Chí Minh",
                // VERIFY coords
                10.7700, 106.6989,
                PlaceCategory.Museum,
                "Quận 1",
                "97A Phó Đức Chính, Phường Nguyễn Thái Bình, Quận 1, TP. Hồ Chí Minh",
                "Bảo tàng Mỹ thuật TP. Hồ Chí Minh được đặt trong một tòa biệt thự cổ theo kiến trúc Pháp - Hoa pha trộn độc đáo, từng là dinh thự của một gia đình thương gia người Hoa giàu có bậc nhất Sài Gòn xưa. Công trình nổi bật với hệ thống cầu thang gỗ chạm khắc tinh xảo, cửa sắt uốn hoa văn nhập từ Pháp, hệ thống thông gió và lấy sáng thông minh phù hợp khí hậu nhiệt đới. Bên trong bảo tàng trưng bày hàng nghìn tác phẩm hội họa, điêu khắc, gốm sứ và nghệ thuật dân gian Việt Nam qua nhiều thời kỳ, từ cổ đại đến hiện đại, bao gồm nhiều tác phẩm của các danh họa nổi tiếng. Không gian kiến trúc độc đáo cùng ánh sáng tự nhiên tuyệt đẹp khiến nơi đây trở thành điểm chụp ảnh yêu thích của giới trẻ bên cạnh giá trị nghệ thuật, lịch sử sâu sắc.",
                30,
                "SAMPLE DATA: 08:00 - 17:00, nghỉ thứ Hai hàng tuần",
                "SAMPLE DATA: 30.000 VNĐ/người lớn",
                "https://picsum.photos/seed/bao-tang-my-thuat/800/600",
                "bao-tang,nghe-thuat,kien-truc-co",
                4.5),
            new(
                "Chùa Ngọc Hoàng",
                // VERIFY coords
                10.7915, 106.6975,
                PlaceCategory.Religious,
                "Quận 1",
                "73 Mai Thị Lựu, Phường Đa Kao, Quận 1, TP. Hồ Chí Minh",
                "Chùa Ngọc Hoàng, còn gọi là Phước Hải Tự, là một trong những ngôi chùa cổ linh thiêng và đặc sắc nhất của người Hoa tại Sài Gòn, được xây dựng vào đầu thế kỷ 20 để thờ Ngọc Hoàng Thượng Đế theo tín ngưỡng dân gian Trung Hoa. Kiến trúc chùa mang đậm phong cách Hoa truyền thống với mái ngói âm dương, các hoa văn chạm khắc tinh xảo bằng gỗ và giấy bồi, cùng không gian khói hương trầm mặc tạo cảm giác huyền bí, cổ kính. Chùa nổi tiếng với tín ngưỡng cầu con, cầu duyên và từng được Tổng thống Mỹ Barack Obama ghé thăm trong một chuyến công tác, góp phần đưa địa danh này đến gần hơn với du khách quốc tế. Khuôn viên chùa còn có hồ nuôi rùa và cá, là nơi người dân thường đến thả rùa cầu bình an, sức khỏe.",
                40,
                "SAMPLE DATA: 07:00 - 18:00 hàng ngày",
                "SAMPLE DATA: miễn phí tham quan",
                "https://picsum.photos/seed/chua-ngoc-hoang/800/600",
                "chua,ton-giao,tin-nguong,kien-truc-hoa",
                4.5),
            new(
                "Nhà hát Thành phố",
                // VERIFY coords
                10.7765, 106.7030,
                PlaceCategory.Entertainment,
                "Quận 1",
                "07 Công Trường Lam Sơn, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Nhà hát Thành phố, hay Nhà hát Lớn Sài Gòn, được xây dựng từ năm 1898 theo phong cách kiến trúc flamboyant đặc trưng thời Pháp thuộc, lấy cảm hứng từ các nhà hát opera châu Âu cổ điển với mặt tiền trang trí tượng đắp nổi và các chi tiết điêu khắc công kỳ công. Nằm ngay giữa trung tâm Quận 1, kế bên khách sạn Continental và phố đi bộ Nguyễn Huệ, nhà hát là nơi tổ chức thường xuyên các buổi biểu diễn nghệ thuật đỉnh cao như múa rối nước, xòe dân gian, nhạc giao hưởng, cải lương và các show trình diễn văn hóa Việt Nam dành cho du khách quốc tế như À Ố Show. Về đêm, công trình được chiếu sáng lộng lẫy, trở thành một trong những background chụp ảnh được yêu thích nhất khi dạo quanh khu vực trung tâm thành phố.",
                50,
                "SAMPLE DATA: lịch diễn thay đổi theo show, thường 20:00",
                "SAMPLE DATA: giá vé show nghệ thuật từ 400.000 VNĐ",
                "https://picsum.photos/seed/nha-hat-thanh-pho/800/600",
                "nha-hat,kien-truc-phap,nghe-thuat,ve-dem",
                4.6),
            new(
                "Bitexco Financial Tower",
                // VERIFY coords
                10.7716, 106.7043,
                PlaceCategory.Landmark,
                "Quận 1",
                "02 Hải Triều, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
                "Bitexco Financial Tower là một trong những tòa tháp mang tính biểu tượng của TP. Hồ Chí Minh, nổi bật với thiết kế độc đáo lấy cảm hứng từ hình ảnh búp sen - loài hoa quốc gia của Việt Nam, cùng với sân đậu trực thăng vươn ra khỏi thân tòa nhà ở độ cao hơn 190 mét, một chi tiết kiến trúc hiếm thấy trên thế giới. Tòa tháp cao 68 tầng, là nơi đặt văn phòng của nhiều doanh nghiệp lớn, trung tâm thương mại cao cấp Icon68 và đặc biệt là đài quan sát Saigon Skydeck ở tầng 49, nơi du khách có thể ngắm toàn cảnh thành phố, dòng sông Sài Gòn uốn lượn và những tòa cao tầng lân cận từ trên cao. Vào ban đêm, tòa tháp rực sáng góp phần tạo nên đường chân trời hiện đại, lộng lẫy cho khu trung tâm tài chính của thành phố.",
                50,
                "SAMPLE DATA: đài quan sát Saigon Skydeck mở 09:30 - 21:30",
                "SAMPLE DATA: 200.000 VNĐ/người lớn (đài quan sát)",
                "https://picsum.photos/seed/bitexco/800/600",
                "toa-nha-cao-tang,bieu-tuong,view-thanh-pho",
                4.5),
        };

        return definitions
            .Select(d => d.ToPlace(now))
            .ToList();
    }

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
        double Rating)
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
            ReviewCount = 0,
            Status = PlaceStatus.Published,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}
