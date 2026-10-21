import type { PlaceDto } from "../api/types";

type FactLang = "en" | "ja" | "ko" | "zh";

interface PlaceFacts {
  hours: string;
  tickets: string;
}

const FACTS: Record<string, Record<FactLang, PlaceFacts>> = {
  "Dinh Độc Lập": {
    en: {
      hours: "Daily 07:00–18:00 (including public holidays and Tết, unless otherwise announced)",
      tickets:
        "Full ticket (Palace + exhibition): 80,000 VND (ages 18–59); 40,000 VND (60+ / Vietnamese students); 20,000 VND (ages 6–17). Limited ticket: 40,000 / 20,000 / 10,000 VND. Shuttle: 25,000 VND/ride (from 01/01/2026).",
    },
    ja: {
      hours: "毎日 07:00–18:00（祝日・テトも含む。特別なお知らせがある場合を除く）",
      tickets:
        "通し券（宮殿＋展示）：80,000 VND（18–59歳）；40,000 VND（60歳以上／ベトナム人学生）；20,000 VND（6–17歳）。限定券：40,000 / 20,000 / 10,000 VND。シャトル：25,000 VND／回（2026/01/01から）。",
    },
    ko: {
      hours: "매일 07:00–18:00 (공휴일·뗏 포함, 별도 공지 시 제외)",
      tickets:
        "통합권(궁전+전시): 80,000 VND(18–59세); 40,000 VND(60세 이상/베트남 학생); 20,000 VND(6–17세). 제한권: 40,000 / 20,000 / 10,000 VND. 셔틀: 25,000 VND/회 (2026.01.01부터).",
    },
    zh: {
      hours: "每天 07:00–18:00（含公休日与春节，除非另有通知）",
      tickets:
        "通票（独立宫+展览）：80,000 越盾（18–59岁）；40,000 越盾（60岁以上/越南学生）；20,000 越盾（6–17岁）。限时票：40,000 / 20,000 / 10,000 越盾。电瓶车：25,000 越盾/次（自 2026/01/01）。",
    },
  },
  "Nhà thờ Đức Bà": {
    en: {
      hours: "Exterior viewing is free; the interior is under long-term restoration and indoor access follows diocese notices",
      tickets: "Free to visit the exterior / square",
    },
    ja: {
      hours: "外観は自由に見学可。内部は長期修復中で、司教区の案内に従って入場が制限されます",
      tickets: "外観／広場の見学は無料",
    },
    ko: {
      hours: "외관 관람은 자유; 내부는 장기 보수 중이며 교구 안내에 따라 출입이 제한됩니다",
      tickets: "외관/광장 관람 무료",
    },
    zh: {
      hours: "外观可自由参观；内部长期修缮，是否进入以内殿堂公告为准",
      tickets: "外观/广场免费参观",
    },
  },
  "Bưu điện Trung tâm Sài Gòn": {
    en: {
      hours: "Mon–Fri 07:00–19:00; Sat 07:00–18:00; Sun 08:00–18:00 (VisithCMC)",
      tickets: "Free to visit; postal services follow VNPost rates",
    },
    ja: {
      hours: "月–金 07:00–19:00；土 07:00–18:00；日 08:00–18:00（VisithCMC）",
      tickets: "見学無料。郵便サービスは VNPost の料金表に従います",
    },
    ko: {
      hours: "월–금 07:00–19:00; 토 07:00–18:00; 일 08:00–18:00 (VisithCMC)",
      tickets: "관람 무료. 우편 서비스는 VNPost 요금표 따름",
    },
    zh: {
      hours: "周一至周五 07:00–19:00；周六 07:00–18:00；周日 08:00–18:00（VisithCMC）",
      tickets: "参观免费；邮政服务按 VNPost 价目表",
    },
  },
  "Chợ Bến Thành": {
    en: {
      hours: "Main market 07:00–19:00 daily; night market 19:00–24:00",
      tickets: "Free entry; stall prices are negotiable",
    },
    ja: {
      hours: "本市場：毎日 07:00–19:00；ナイトマーケット：19:00–24:00",
      tickets: "入場無料。店の値段は交渉可",
    },
    ko: {
      hours: "본시장 매일 07:00–19:00; 야시장 19:00–24:00",
      tickets: "입장 무료. 물건값은 흥정 가능",
    },
    zh: {
      hours: "主市场每天 07:00–19:00；夜市 19:00–24:00",
      tickets: "免费入场；摊位价格可议",
    },
  },
  "Bảo tàng Chứng tích Chiến tranh": {
    en: {
      hours: "Daily 07:30–17:30 (ticket desk until 17:00)",
      tickets:
        "40,000 VND/adult; discounts or free for eligible groups (children under 6 free). Source: baotangchungtichchientranh.vn",
    },
    ja: {
      hours: "毎日 07:30–17:30（チケット窓口は 17:00 まで）",
      tickets: "大人 40,000 VND。対象者は減免（6歳未満無料）。出典: baotangchungtichchientranh.vn",
    },
    ko: {
      hours: "매일 07:30–17:30 (매표는 17:00까지)",
      tickets: "성인 40,000 VND. 대상자 감면/무료(6세 미만 무료). 출처: baotangchungtichchientranh.vn",
    },
    zh: {
      hours: "每天 07:30–17:30（售票至 17:00）",
      tickets: "成人 40,000 越盾；符合条件者减免（6岁以下免费）。来源：baotangchungtichchientranh.vn",
    },
  },
  "Phố đi bộ Nguyễn Huệ": {
    en: {
      hours: "Open 24/7; liveliest around 18:00–22:00",
      tickets: "Free",
    },
    ja: {
      hours: "24時間開放。いちばん賑わうのは 18:00–22:00 頃",
      tickets: "無料",
    },
    ko: {
      hours: "24시간 개방. 가장 붐비는 시간은 18:00–22:00",
      tickets: "무료",
    },
    zh: {
      hours: "全天开放；约 18:00–22:00 最热闹",
      tickets: "免费",
    },
  },
  "Landmark 81": {
    en: {
      hours: "SkyView observatory: daily 10:00–21:30 (Vincom)",
      tickets:
        "SkyView Mon–Fri: 300,000 VND (>1.4 m) / 150,000 VND (<1.4 m). Sat–Sun & holidays: 500,000 / 250,000 VND. Source: vincom.com.vn",
    },
    ja: {
      hours: "SkyView 展望台：毎日 10:00–21:30（Vincom）",
      tickets:
        "SkyView 月–金：300,000 VND（身長 1.4 m超）／150,000 VND（1.4 m未満）。土日・祝：500,000 / 250,000 VND。出典: vincom.com.vn",
    },
    ko: {
      hours: "SkyView 전망대: 매일 10:00–21:30 (Vincom)",
      tickets:
        "SkyView 월–금: 300,000 VND(키 1.4 m 초과) / 150,000 VND(1.4 m 미만). 토·일·공휴일: 500,000 / 250,000 VND. 출처: vincom.com.vn",
    },
    zh: {
      hours: "SkyView 观景台：每天 10:00–21:30（Vincom）",
      tickets:
        "SkyView 周一至周五：300,000 越盾（身高 >1.4 m）／150,000 越盾（<1.4 m）。周六日及节假日：500,000 / 250,000 越盾。来源：vincom.com.vn",
    },
  },
  "Thảo Cầm Viên Sài Gòn": {
    en: {
      hours: "Daily 07:00–18:00 (ticket sales stop at 17:30)",
      tickets:
        "Height from 1.3 m: 60,000 VND; 1.0–1.3 m: 40,000 VND; under 1.0 m (with a parent): free. Source: saigonzoo.vn",
    },
    ja: {
      hours: "毎日 07:00–18:00（チケット販売は 17:30 終了）",
      tickets:
        "身長 1.3 m以上：60,000 VND；1.0–1.3 m：40,000 VND；1.0 m未満（保護者同伴）：無料。出典: saigonzoo.vn",
    },
    ko: {
      hours: "매일 07:00–18:00 (매표 17:30 마감)",
      tickets:
        "키 1.3 m 이상: 60,000 VND; 1.0–1.3 m: 40,000 VND; 1.0 m 미만(보호자 동반): 무료. 출처: saigonzoo.vn",
    },
    zh: {
      hours: "每天 07:00–18:00（17:30 停止售票）",
      tickets:
        "身高 1.3 m 起：60,000 越盾；1.0–1.3 m：40,000 越盾；1.0 m 以下（需家长陪同）：免费。来源：saigonzoo.vn",
    },
  },
  "Chợ Lớn (Chợ Bình Tây)": {
    en: {
      hours: "About 06:00–18:00 daily (busiest in the morning)",
      tickets: "Free entry; stall prices are negotiable",
    },
    ja: {
      hours: "毎日おおよそ 06:00–18:00（午前がいちばん賑やか）",
      tickets: "入場無料。店の値段は交渉可",
    },
    ko: {
      hours: "매일 약 06:00–18:00 (오전이 가장 붐빔)",
      tickets: "입장 무료. 물건값은 흥정 가능",
    },
    zh: {
      hours: "每天约 06:00–18:00（上午最热闹）",
      tickets: "免费入场；摊位价格可议",
    },
  },
  "Địa đạo Củ Chi": {
    en: {
      hours: "Daily 07:00–17:00",
      tickets:
        "International visitors (from 01/02/2026): 35,000 VND admission + 100,000 VND service/guide ticket. Domestic visitors: on-site rates. Source: diadaocuchi.com.vn",
    },
    ja: {
      hours: "毎日 07:00–17:00",
      tickets:
        "外国人（2026/02/01から）：入場 35,000 VND ＋案内・サービス券 100,000 VND。国内客は現地料金。出典: diadaocuchi.com.vn",
    },
    ko: {
      hours: "매일 07:00–17:00",
      tickets:
        "외국인(2026.02.01부터): 입장 35,000 VND + 안내/서비스권 100,000 VND. 내국인은 현장 요금. 출처: diadaocuchi.com.vn",
    },
    zh: {
      hours: "每天 07:00–17:00",
      tickets:
        "国际游客（自 2026/02/01）：门票 35,000 越盾 + 服务/讲解票 100,000 越盾。国内游客按现场价。来源：diadaocuchi.com.vn",
    },
  },
  "Rừng ngập mặn Cần Giờ": {
    en: {
      hours: "Typical visiting hours about 07:00–17:00 (varies by site in the reserve)",
      tickets:
        "Entry to eco sites (Monkey Island / Rừng Sác) at the gate — usually tens of thousands of VND/person; private boat hire extra",
    },
    ja: {
      hours: "一般的な見学時間は 07:00–17:00 頃（保護区内の地点により異なる）",
      tickets:
        "生態スポット（猿島／Rừng Sác）は門前料金。一人あたり数万 VND 程度。ボートの貸切は別途",
    },
    ko: {
      hours: "일반 관람 시간은 약 07:00–17:00 (보호구역 내 지점에 따라 다름)",
      tickets:
        "생태 지점(원숭이섬 / Rừng Sác)은 현장에서 매표 — 보통 1인 수만 VND; 전용 보트는 별도",
    },
    zh: {
      hours: "常见参观时间约 07:00–17:00（保护区内各点不同）",
      tickets: "生态点（猴岛／Rừng Sác）门口购票——通常每人数万越盾；包船另计",
    },
  },
  "Bảo tàng Mỹ thuật TP. Hồ Chí Minh": {
    en: {
      hours: "Daily 08:00–17:00 (check notices for individual galleries)",
      tickets: "About 40,000 VND/adult (at the ticket desk; student discounts available)",
    },
    ja: {
      hours: "毎日 08:00–17:00（展示室ごとの休室案内を確認）",
      tickets: "大人おおよそ 40,000 VND（窓口料金。学生割引あり）",
    },
    ko: {
      hours: "매일 08:00–17:00 (전시실별 휴관 공지 확인)",
      tickets: "성인 약 40,000 VND (현장 요금, 학생 할인)",
    },
    zh: {
      hours: "每天 08:00–17:00（请留意各展厅关闭公告）",
      tickets: "成人约 40,000 越盾（售票处价格；学生优惠）",
    },
  },
  "Chùa Ngọc Hoàng": {
    en: {
      hours: "About 07:00–18:00 daily (incense hours may run later)",
      tickets: "Free to visit / donations welcome",
    },
    ja: {
      hours: "毎日おおよそ 07:00–18:00（焼香の時間は延びることがあります）",
      tickets: "見学無料／志納は任意",
    },
    ko: {
      hours: "매일 약 07:00–18:00 (분향 시간은 더 길어질 수 있음)",
      tickets: "관람 무료 / 시주는 자율",
    },
    zh: {
      hours: "每天约 07:00–18:00（上香时间可能延长）",
      tickets: "免费参观 / 随喜功德",
    },
  },
  "Nhà hát Thành phố": {
    en: {
      hours: "Exterior viewing is free; showtimes vary (usually evenings)",
      tickets:
        "Free to view the façade; performance tickets (À Ố Show / ballet, etc.) per show, typically from a few hundred thousand VND",
    },
    ja: {
      hours: "外観は自由に見学可。公演時間は演目による（多くは夜）",
      tickets: "外観見学は無料。公演チケット（À Ố Show／バレエなど）は公演ごとに、通常数十万 VND から",
    },
    ko: {
      hours: "외관 관람은 자유. 공연 시간은 회차마다 다름(보통 저녁)",
      tickets: "외관 관람 무료. 공연 티켓(À Ố Show / 발레 등)은 회차별, 보통 수십만 VND부터",
    },
    zh: {
      hours: "外观可自由参观；演出时间按场次（多为晚上）",
      tickets: "外观免费；演出票（À Ố Show / 芭蕾等）按场次，通常从数十万越盾起",
    },
  },
  "Bitexco Financial Tower": {
    en: {
      hours: "Saigon Skydeck: daily 09:30–21:30 (last tickets ~45 minutes before closing)",
      tickets:
        "Adults: 240,000 VND; children 4–12 / seniors 65+ / people with disabilities: 160,000 VND; under 4 free (with an adult). Source: bitexcofinancialtower.com",
    },
    ja: {
      hours: "Saigon Skydeck：毎日 09:30–21:30（最終券は閉館約 45 分前）",
      tickets:
        "大人 240,000 VND；4–12歳／65歳以上／障害者 160,000 VND；4歳未満は無料（大人同伴）。出典: bitexcofinancialtower.com",
    },
    ko: {
      hours: "Saigon Skydeck: 매일 09:30–21:30 (마감 약 45분 전 마지막 매표)",
      tickets:
        "성인 240,000 VND; 어린이 4–12세 / 65세 이상 / 장애인 160,000 VND; 4세 미만 무료(성인 동반). 출처: bitexcofinancialtower.com",
    },
    zh: {
      hours: "Saigon Skydeck：每天 09:30–21:30（闭馆约 45 分钟前停止售票）",
      tickets:
        "成人 240,000 越盾；4–12岁 / 65岁以上 / 残障人士 160,000 越盾；4岁以下免费（需成人陪同）。来源：bitexcofinancialtower.com",
    },
  },
};

export function localizePlaceFacts(
  place: Pick<PlaceDto, "name" | "openingHours" | "ticketPrice">,
  lang: string,
): PlaceFacts {
  const code = lang.slice(0, 2).toLowerCase();
  if (code === "vi") {
    return { hours: place.openingHours, tickets: place.ticketPrice };
  }
  if (code === "en" || code === "ja" || code === "ko" || code === "zh") {
    const row = FACTS[place.name]?.[code];
    if (row) return row;
  }
  return { hours: place.openingHours, tickets: place.ticketPrice };
}
