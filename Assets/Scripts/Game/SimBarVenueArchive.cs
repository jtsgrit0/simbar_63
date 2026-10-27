/// <summary>Selected dated venue records. Not a full history or current booking availability.</summary>
public static class SimBarVenueArchive
{
    public const string VerifiedAt = "2026-10-27";
    public sealed class Entry
    {
        public readonly string venueId, artist, date, url, evidence;
        public Entry(string venueId, string artist, string date, string url, string evidence)
        { this.venueId=venueId; this.artist=artist; this.date=date; this.url=url; this.evidence=evidence; }
    }
    public static readonly Entry[] Entries =
    {
        new Entry("ff", "Dragon Pony (드래곤포니)", "2024-11-01", "https://world.nol.com/en/ticket/places/14001580/products/24014919", "past-announcement"),
        new Entry("ff", "크라잉넛 (CRYING NUT)", "2025-06-26", "https://ticket.melon.com/performance/index.htm?prodId=211483", "past-announcement"),
        new Entry("ff", "심아일랜드", "2025-06-26", "https://ticket.melon.com/performance/index.htm?prodId=211483", "past-announcement"),
        new Entry("ff", "서울부인", "2025-06-26", "https://ticket.melon.com/performance/index.htm?prodId=211483", "past-announcement"),
        new Entry("ff", "creespy (크리스피)", "2025-11-19", "https://ticket.melon.com/performance/index.htm?prodId=212333", "past-announcement"),
        new Entry("ff", "can’t be blue (캔트비블루)", "2025-12-12", "https://ticket.melon.com/performance/index.htm?prodId=212368", "past-announcement"),
        new Entry("ff", "ChRocktikal (크록티칼)", "2026-01-28", "https://ticket.melon.com/csoon/detail.htm?csoonId=11517", "past-announcement"),
        new Entry("ff", "스토리셀러 (Storyseller)", "2026-09-12", "https://ticket.melon.com/performance/index.htm?prodId=213732", "past-announcement"),
        new Entry("pet-sounds", "LUVLIMINALL", "2025-07-05", "https://yowll.jp/2025-07-05/", "past-announcement / artist-archive"),
        new Entry("pet-sounds", "YOWLL", "2025-07-05", "https://yowll.jp/2025-07-05/", "past-announcement / artist-archive"),
        new Entry("pet-sounds", "RE:NIER", "2025-07-05", "https://yowll.jp/2025-07-05/", "past-announcement / artist-archive"),
        new Entry("pet-sounds", "형태 (HYUNGTAE)", "2026-10-16", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("pet-sounds", "애니멀다이버스 (Animal Divers)", "2026-10-16", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("pet-sounds", "기프트 (GIFT)", "2026-10-16", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("pet-sounds", "스타트라인 (STARTLINE)", "2026-10-17", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("pet-sounds", "럭스 (RUX)", "2026-10-17", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("pet-sounds", "해리빅버튼 (HarryBigButton)", "2026-10-17", "https://gndloop.kr/shows", "past-announcement"),
        new Entry("boogie-woogie", "Henk Kraaijeveld", "2025-11-10", "https://dutchculture.nl/en/organisation/henk-kraaijeveld", "past-record"),
        new Entry("boogie-woogie", "Sam Ghezzi", "2025-10-22", "https://dutchculture.nl/en/organisation/sam-ghezzi", "past-record"),
        new Entry("boogie-woogie", "Tabea & Grace Quartet", "2024-10-15", "https://tabeaandgrace.com/index.html", "artist-tour-archive"),
        new Entry("boogie-woogie", "NOHMI Quintet", "2024-05-24", "https://dutchculture.nl/en/location/boogie-woogie-seoul", "past-record"),
        new Entry("boogie-woogie", "Judith Nijland", "2023-11-05", "https://judithnijland.nl/nl/live/", "artist-tour-archive"),
        new Entry("boogie-woogie", "Sebastiaan Kaptein", "2023-11-05", "https://dutchculture.nl/en/location/boogie-woogie-seoul", "past-record"),
        new Entry("boogie-woogie", "Sven Rozier", "2019-09-12", "https://dutchculture.nl/en/location/boogie-woogie-seoul", "past-record"),
        new Entry("boogie-woogie", "Saskia Laroo", "2017-09-22", "https://www.jazznearyou.com/seoul/events/saskia-laroos-jazzkia-at-19-00-on-september-22", "past-announcement"),
        new Entry("subriot", "Rux", "2025-09-20", "https://www.koreatimes.co.kr/southkorea/globalcommunity/20250925/photos-music-and-arts-festival-rocks-the-block-in-central-seouls-haebangchon", "retrospective-press-photo"),
        new Entry("subriot", "18Fevers", "2025-09-20", "https://www.koreatimes.co.kr/southkorea/globalcommunity/20260901/in-its-5th-year-block-party-wants-to-bring-dank-back-to-haebangchon", "retrospective-press-photo"),
        new Entry("subriot", "Asian Spice House", "2025-09-20", "https://www.daehanmindecline.com/2025/blockparty.html", "firsthand-photo-report"),
        new Entry("subriot", "Hammering", "2025-09-21", "https://www.daehanmindecline.com/2025/blockparty.html", "firsthand-photo-report"),
        new Entry("subriot", "Pop Ents", "2025-09-21", "https://www.koreatimes.co.kr/southkorea/globalcommunity/20250925/photos-music-and-arts-festival-rocks-the-block-in-central-seouls-haebangchon", "retrospective-press-photo"),
        new Entry("subriot", "잭킹콩 (JKC / JACKINGCONG)", "2025-10-25", "https://www.stagepick.co.kr/performances/detail/204722", "past-announcement + fan-setlist"),
        new Entry("subriot", "Route 25", "2026-02-01", "https://muzikey.com/events/c3e38004-437b-415f-aab8-402b196aa076", "past-announcement"),
        new Entry("subriot", "BLUE MESSENGERS", "2026-02-01", "https://muzikey.com/events/c3e38004-437b-415f-aab8-402b196aa076", "past-announcement"),
    };
}
