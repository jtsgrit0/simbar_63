using System;

/// <summary>
/// Deterministic daytime booking rules. Credits are game currency (1 credit = KRW 1,000).
/// The first twelve artists are original fictional newcomers. Appended real artist names
/// come from visually checked poster records; their fees, draw, relationships and acceptance
/// probabilities remain GAME PARAMETERS and are not real-world booking conditions.
/// The caller applies Result.cashDelta to its wallet and saves the receipt atomically.
/// </summary>
public static class SimBarBooking
{
    public const int ActionLimit = 4;
    public const int BookingLimit = 3;
    public const int LegacyBandCount = 12;
    public const float PromotionCost = 60f;
    private const int LegacyBandMask = (1 << LegacyBandCount) - 1;
    private static int BandMask { get { return (1 << BandCount) - 1; } }
    private static int PosterMask
    {
        get
        {
            int count = SimBarPosterCatalog.Entries == null ? 0 : SimBarPosterCatalog.Entries.Length;
            if (count >= 31) throw new InvalidOperationException("Poster scouting supports at most 30 stable poster indices.");
            return (1 << count) - 1;
        }
    }

    public sealed class Venue
    {
        public readonly string id, name, nameEnglish, preferredGenre;
        public readonly int capacity;
        internal readonly int alternateGenre;

        internal Venue(string id, string name, string nameEnglish, int capacity, int preferred, int alternate)
        {
            this.id = id; this.name = name; this.nameEnglish = nameEnglish;
            this.capacity = capacity; preferredGenre = GenreNames[preferred]; alternateGenre = alternate;
        }
    }

    public sealed class Band
    {
        public readonly string id, name, nameEnglish, genre, genreKorean, sourceUrl;
        public readonly bool isReal;
        public readonly int baseFee, draw;
        internal readonly int genreIndex;

        internal Band(string id, string name, string english, int genreIndex, int fee, int draw, bool isReal = false, string sourceUrl = "")
        {
            this.id = id; this.name = name; nameEnglish = english; this.genreIndex = genreIndex;
            bool knownGenre = genreIndex >= 0 && genreIndex < GenreNames.Length;
            genre = knownGenre ? GenreNames[genreIndex] : "Unverified genre";
            genreKorean = knownGenre ? GenreNamesKorean[genreIndex] : "장르 미확인";
            baseFee = fee; this.draw = draw; this.isReal = isReal; this.sourceUrl = sourceUrl ?? "";
        }
    }

    private static readonly string[] GenreNames =
    {
        "Rock", "Jazz", "Punk", "Hip-hop", "Electronic", "Folk", "Metal", "Shoegaze", "R&B", "Post-rock", "Experimental", "Latin", "Blues"
    };
    private static readonly string[] GenreNamesKorean =
    {
        "록", "재즈", "펑크", "힙합", "일렉트로닉", "포크", "메탈", "슈게이즈", "알앤비", "포스트록", "실험음악", "라틴", "블루스"
    };

    // These four capacities and preferences describe this game, not the actual venues.
    public static readonly Venue[] Venues4 =
    {
        new Venue("ff", "홍대 FF", "Hongdae FF", 110, 0, 2),
        new Venue("pet-sounds", "이태원 펫사운즈", "Itaewon Pet Sounds", 80, 5, 8),
        new Venue("boogie-woogie", "부기우기", "Boogie Woogie", 70, 1, 11),
        new Venue("subriot", "해방촌 서브라이엇", "Haebangchon Subriot", 90, 4, 10)
    };

    // Original fictional newcomers; no real artists, biographies or audience statistics.
    private static readonly Band[] LegacyBands =
    {
        new Band("amp-sprout-17", "앰프새싹 17", "Amp Sprout 17", 0, 100, 14),
        new Band("lamp-rabbit-quartet", "램프토끼 사중주", "Lamp Rabbit Quartet", 1, 120, 15),
        new Band("paper-siren-09", "종이사이렌 09", "Paper Siren 09", 2, 80, 12),
        new Band("rooftop-syllable", "옥상음절 실험실", "Rooftop Syllable Lab", 3, 120, 15),
        new Band("pixel-tide-27", "픽셀조수 27", "Pixel Tide 27", 4, 140, 16),
        new Band("pocket-orchard", "주머니과수원 합주단", "Pocket Orchard Ensemble", 5, 80, 12),
        new Band("tin-meteor-06", "양철유성 06", "Tin Meteor 06", 6, 160, 17),
        new Band("velvet-rain-check", "벨벳우산 연습실", "Velvet Umbrella Rehearsal", 7, 120, 14),
        new Band("honey-voltage-32", "꿀빛전압 32", "Honey Voltage 32", 8, 140, 16),
        new Band("slow-bridge-11", "느린다리 11호", "Slow Bridge No. 11", 9, 180, 18),
        new Band("odd-spoon-orbit", "홀수숟가락 궤도", "Odd Spoon Orbit", 10, 220, 16),
        new Band("lime-lantern-04", "라임등불 04", "Lime Lantern 04", 11, 100, 14)
    };

    // Compatibility name retained for callers. Keep every existing index append-only;
    // indices 0..11 are the legacy fictional artists from older saved games.
    public static readonly Band[] Bands12 = BuildBands();
    public static int BandCount { get { return Bands12.Length; } }

    private static Band[] BuildBands()
    {
        var artists = SimBarPosterCatalog.Artists;
        int extra = artists == null ? 0 : artists.Length;
        if (LegacyBandCount + extra >= 31)
            throw new InvalidOperationException("Booking supports at most 30 stable artist indices; preserve the first twelve.");
        var result = new Band[LegacyBandCount + extra];
        Array.Copy(LegacyBands, result, LegacyBandCount);
        for (int i = 0; i < extra; i++)
        {
            var artist = artists[i];
            if (artist == null || string.IsNullOrWhiteSpace(artist.id) || string.IsNullOrWhiteSpace(artist.name))
                throw new InvalidOperationException("Poster artist definitions need a stable id and a verified display name.");
            for (int previous = 0; previous < LegacyBandCount + i; previous++)
                if (result[previous].id == artist.id) throw new InvalidOperationException("Duplicate booking artist id: " + artist.id);
            result[LegacyBandCount + i] = new Band(artist.id, artist.name,
                string.IsNullOrWhiteSpace(artist.nameEnglish) ? artist.name : artist.nameEnglish,
                artist.genreIndex, Clamp(artist.gameFee, 20, 500), Clamp(artist.gameDraw, 1, 50), true, artist.sourceUrl);
        }
        return result;
    }

    [Serializable]
    public sealed class Plan
    {
        public int day = 1;
        public int venue;
        public int blocks;
        public int listenedMask;
        public int attemptedMask;
        public int bookedMask;
        public int scoutedPosterMask;
        public int promotions;
        public int ticketTier = 1;
        public bool locked;
        public bool completed;
        public int attempts;
        // Capture career growth when an offer is accepted so the evening stays stable.
        public int[] bookedGrowth = new int[BandCount];
        public bool hasLockedMetrics;
        public Metrics lockedMetrics;
    }

    [Serializable]
    public sealed class Career
    {
        public int discoveredMask = LegacyBandMask;
        public int[] relationships = new int[BandCount];
        public int[] growth = new int[BandCount];
    }

    [Serializable]
    public struct Metrics
    {
        public int attendance;
        public float ticketPrice;
        public float ticketIncome;
        public float energyBonus;
        public int fanBonus;
        public float orderIntervalMultiplier;
        public int bookedCount;
        public int venueCapacity;
    }

    public struct Result
    {
        public bool success;
        public string code;
        public float cashDelta;
        public int bandIndex;
        public int attendance;
        public float income;
    }

    public static Plan NewPlan(int day, int venueIndex)
    {
        return new Plan { day = Math.Max(1, day), venue = Clamp(venueIndex, 0, Venues4.Length - 1) };
    }

    public static Career NewCareer() { return new Career(); }

    /// <summary>Repair loaded state only. Action methods never repair or spend on invalid input.</summary>
    public static void Sanitize(Plan plan, Career career)
    {
        if (career != null)
        {
            career.relationships = RepairArray(career.relationships);
            career.growth = RepairArray(career.growth);
            // Old JSON has no discovery field; the original roster stays available.
            career.discoveredMask = (career.discoveredMask & BandMask) | LegacyBandMask;
        }
        if (plan == null) return;
        plan.day = Math.Max(1, plan.day);
        plan.venue = Clamp(plan.venue, 0, Venues4.Length - 1);
        plan.ticketTier = Clamp(plan.ticketTier, 0, 2);
        plan.bookedMask = FirstBits(plan.bookedMask & BandMask, BookingLimit);
        int attempted = (plan.attemptedMask & BandMask) | plan.bookedMask;
        plan.attemptedMask = plan.bookedMask | FirstBits(attempted & ~plan.bookedMask, ActionLimit - Count(plan.bookedMask));
        plan.attempts = Count(plan.attemptedMask);
        plan.listenedMask = FirstBits(plan.listenedMask & BandMask, ActionLimit - plan.attempts);
        plan.scoutedPosterMask = FirstBits(plan.scoutedPosterMask & PosterMask,
            ActionLimit - plan.attempts - Count(plan.listenedMask));
        plan.promotions = Clamp(plan.promotions, 0, ActionLimit - plan.attempts - Count(plan.listenedMask) - Count(plan.scoutedPosterMask));
        int spent = plan.attempts + Count(plan.listenedMask) + plan.promotions + Count(plan.scoutedPosterMask);
        plan.blocks = Clamp(Math.Max(plan.blocks, spent), 0, ActionLimit);
        plan.bookedGrowth = RepairArray(plan.bookedGrowth);
        for (int i = 0; i < BandCount; i++) if ((plan.bookedMask & (1 << i)) == 0) plan.bookedGrowth[i] = 0;
        if (career != null)
        {
            career.discoveredMask |= plan.bookedMask | plan.attemptedMask | plan.listenedMask;
            int posterCount = SimBarPosterCatalog.Entries == null ? 0 : SimBarPosterCatalog.Entries.Length;
            for (int i = 0; i < posterCount; i++)
            {
                int artists;
                if ((plan.scoutedPosterMask & (1 << i)) != 0 && TryPosterArtists(i, out artists))
                    career.discoveredMask |= artists;
            }
        }
        if (plan.completed) plan.locked = true;
        if (plan.locked)
        {
            plan.lockedMetrics = plan.hasLockedMetrics ? RepairMetrics(plan.lockedMetrics, plan.venue) : Calculate(plan);
            plan.hasLockedMetrics = true;
        }
        else plan.hasLockedMetrics = false;
    }

    public static int Hour(Plan plan) { return 10 + 2 * (plan == null ? 0 : Clamp(plan.blocks, 0, ActionLimit)); }
    public static bool CanAct(Plan plan)
    {
        return plan != null && !plan.locked && !plan.completed && plan.blocks >= 0 && plan.blocks < ActionLimit;
    }

    public static int Chance(Plan plan, Career career, int band, int package)
    {
        if (!ValidPlan(plan) || !ValidBand(band) || !ValidPackage(package) || !IsDiscovered(career, band)) return 0;
        int chance = 58 + VenueFit(plan.venue, band) + (package == 1 ? 15 : 0);
        if ((plan.listenedMask & (1 << band)) != 0) chance += 20;
        chance += CareerValue(career == null ? null : career.relationships, band) / 12;
        chance += CareerValue(career == null ? null : career.growth, band) / 20;
        return Clamp(chance, 15, 95);
    }

    public static float Fee(int band, int package)
    {
        return ValidBand(band) && ValidPackage(package) ? Bands12[band].baseFee * (package == 1 ? 1.35f : 1f) : 0f;
    }

    public static bool IsDiscovered(Career career, int band)
    {
        return ValidBand(band) && (band < LegacyBandCount || (career != null && (career.discoveredMask & (1 << band)) != 0));
    }

    public static bool IsPosterDiscovered(Career career, int posterIndex)
    {
        int artists;
        return TryPosterArtists(posterIndex, out artists) && career != null
            && (career.discoveredMask & artists) == artists;
    }

    /// <summary>Reviewing a poster costs one two-hour block and permanently unlocks its real artist names.</summary>
    public static Result Scout(Plan plan, Career career, int posterIndex)
    {
        string error = ActionError(plan);
        if (error != null) return Fail(error);
        if (career == null) return Fail("invalid_career");
        int artists;
        if (!TryPosterArtists(posterIndex, out artists)) return Fail("invalid_poster");
        int posterBit = 1 << posterIndex;
        if ((plan.scoutedPosterMask & posterBit) != 0) return Fail("already_scouted");
        if ((career.discoveredMask & artists) == artists) return Fail("already_discovered");
        plan.scoutedPosterMask |= posterBit;
        plan.blocks++;
        career.discoveredMask = (career.discoveredMask | artists | LegacyBandMask) & BandMask;
        return Ok("scouted");
    }

    public static Result Listen(Plan plan, Career career, int band)
    {
        string error = ActionError(plan);
        if (error != null) return Fail(error, band);
        if (!ValidBand(band)) return Fail("invalid_band", band);
        if (!IsDiscovered(career, band)) return Fail("not_discovered", band);
        int bit = 1 << band;
        if ((plan.listenedMask & bit) != 0) return Fail("already_listened", band);
        plan.listenedMask |= bit;
        plan.blocks++;
        return Ok("listened", band);
    }

    public static Result Offer(Plan plan, Career career, int band, int package, float availableCash)
    {
        string error = ActionError(plan);
        if (error != null) return Fail(error, band);
        if (!ValidBand(band)) return Fail("invalid_band", band);
        if (!IsDiscovered(career, band)) return Fail("not_discovered", band);
        if (!ValidPackage(package)) return Fail("invalid_package", band);
        if (!Finite(availableCash)) return Fail("invalid_cash", band);
        int bit = 1 << band;
        if ((plan.attemptedMask & bit) != 0) return Fail("already_attempted", band);
        if (Count(plan.bookedMask) >= BookingLimit) return Fail("lineup_full", band);
        float fee = Fee(band, package);
        if (availableCash < fee) return Fail("insufficient_cash", band);
        int attemptIndex = Count(plan.attemptedMask);
        bool accepted = Roll(plan.day, plan.venue, band, package, attemptIndex) < Chance(plan, career, band, package);
        plan.attemptedMask |= bit;
        plan.attempts = attemptIndex + 1;
        plan.blocks++;
        if (!accepted) return Fail("rejected", band);
        plan.bookedMask |= bit;
        if (plan.bookedGrowth == null || plan.bookedGrowth.Length != BandCount) plan.bookedGrowth = RepairArray(plan.bookedGrowth);
        plan.bookedGrowth[band] = CareerValue(career == null ? null : career.growth, band);
        Result result = Ok("accepted", band);
        result.cashDelta = -fee;
        return result;
    }

    public static Result Promote(Plan plan, Career career, float availableCash)
    {
        string error = ActionError(plan);
        if (error != null) return Fail(error);
        if (!Finite(availableCash)) return Fail("invalid_cash");
        if (availableCash < PromotionCost) return Fail("insufficient_cash");
        plan.promotions++;
        plan.blocks++;
        Result result = Ok("promoted");
        result.cashDelta = -PromotionCost;
        return result;
    }

    public static void SetTicket(Plan plan, int tier)
    {
        if (ValidPlan(plan) && !plan.locked && !plan.completed && tier >= 0 && tier <= 2) plan.ticketTier = tier;
    }

    public static Metrics Forecast(Plan plan, Career career)
    {
        if (!ValidPlan(plan)) return new Metrics { orderIntervalMultiplier = 1f };
        return plan.locked && plan.hasLockedMetrics ? RepairMetrics(plan.lockedMetrics, plan.venue) : Calculate(plan);
    }

    public static void Lock(Plan plan)
    {
        if (!ValidPlan(plan) || (plan.locked && plan.hasLockedMetrics)) return;
        plan.lockedMetrics = Calculate(plan);
        plan.hasLockedMetrics = true;
        plan.locked = true;
    }

    /// <summary>
    /// Once per saved plan: 0/1/2/3 stars pay 70/80/90/100 percent of its frozen ticket forecast.
    /// cashDelta equals income; apply it once together with saving completed and the career.
    /// Open mic earns no ticket money. Artist fees and promotion were already paid during planning.
    /// </summary>
    public static Result Complete(Plan plan, Career career, int stars)
    {
        if (plan == null) return Fail("invalid_plan");
        if (plan.completed) return Fail("already_completed");
        if (!ValidPlan(plan)) return Fail("invalid_plan");
        if (career == null) return Fail("invalid_career");
        Lock(plan);
        Metrics metrics = Forecast(plan, career);
        stars = Clamp(stars, 0, 3);
        career.relationships = RepairArray(career.relationships);
        career.growth = RepairArray(career.growth);
        for (int i = 0; i < BandCount; i++)
        {
            if ((plan.bookedMask & (1 << i)) == 0) continue;
            career.relationships[i] = Clamp(career.relationships[i] + 4 + stars, 0, 100);
            career.growth[i] = Clamp(career.growth[i] + 2 + stars, 0, 100);
        }
        plan.completed = true;
        Result result = Ok("complete");
        result.attendance = metrics.attendance;
        result.income = (float)Math.Floor(metrics.ticketIncome * (70 + stars * 10) / 100d);
        result.cashDelta = result.income;
        return result;
    }

    private static Metrics Calculate(Plan plan)
    {
        int count = Count(plan.bookedMask);
        int capacity = Venues4[plan.venue].capacity;
        int draw = count == 0 ? 12 : 10;
        int fit = 0;
        for (int i = 0; i < BandCount; i++)
        {
            if ((plan.bookedMask & (1 << i)) == 0) continue;
            int affinity = VenueFit(plan.venue, i);
            fit += affinity;
            draw += Bands12[i].draw + affinity / 2 + CareerValue(plan.bookedGrowth, i) / 20;
            if ((plan.listenedMask & (1 << i)) != 0) draw += 2;
        }
        // Lower prices trade income per person for demand. Promotion costs one of four blocks.
        float demand = plan.ticketTier == 0 ? 1.12f : plan.ticketTier == 2 ? 0.80f : 1f;
        int attendance = Clamp((int)Math.Floor(draw * demand) + plan.promotions * 8, 0, capacity);
        float price = 10f + plan.ticketTier * 5f;
        return new Metrics
        {
            attendance = attendance, ticketPrice = price, ticketIncome = count == 0 ? 0f : attendance * price,
            energyBonus = Math.Min(0.15f, count * 0.025f + plan.promotions * 0.01f + fit * 0.001f),
            fanBonus = count == 0 ? 0 : attendance / 14 + count,
            orderIntervalMultiplier = Math.Max(0.75f, 1f - count * 0.025f - plan.promotions * 0.015f - attendance * 0.001f),
            bookedCount = count, venueCapacity = capacity
        };
    }

    private static Metrics RepairMetrics(Metrics metrics, int venue)
    {
        metrics.venueCapacity = Venues4[venue].capacity;
        metrics.attendance = Clamp(metrics.attendance, 0, metrics.venueCapacity);
        metrics.bookedCount = Clamp(metrics.bookedCount, 0, BookingLimit);
        metrics.ticketPrice = ClampFinite(metrics.ticketPrice, 10f, 20f, 15f);
        // Preserve the locked forecast when loading; bound corrupt receipts without inventing income.
        metrics.ticketIncome = metrics.bookedCount == 0 ? 0f : ClampFinite(metrics.ticketIncome, 0f, metrics.attendance * metrics.ticketPrice, 0f);
        metrics.energyBonus = ClampFinite(metrics.energyBonus, 0f, 0.15f, 0f);
        metrics.fanBonus = Clamp(metrics.fanBonus, 0, 12);
        metrics.orderIntervalMultiplier = ClampFinite(metrics.orderIntervalMultiplier, 0.75f, 1f, 1f);
        return metrics;
    }

    private static int VenueFit(int venue, int band)
    {
        Venue definition = Venues4[venue];
        return definition.preferredGenre == Bands12[band].genre ? 8 : definition.alternateGenre == Bands12[band].genreIndex ? 4 : 0;
    }

    private static string ActionError(Plan plan)
    {
        if (!ValidPlan(plan)) return "invalid_plan";
        if (plan.completed) return "already_completed";
        if (plan.locked) return "locked";
        return CanAct(plan) ? null : "no_time";
    }

    private static bool ValidPlan(Plan plan)
    {
        return plan != null && plan.day >= 1 && plan.venue >= 0 && plan.venue < Venues4.Length
            && plan.blocks >= 0 && plan.blocks <= ActionLimit && plan.promotions >= 0 && plan.promotions <= ActionLimit
            && plan.ticketTier >= 0 && plan.ticketTier <= 2 && (plan.listenedMask & ~BandMask) == 0
            && (plan.attemptedMask & ~BandMask) == 0 && (plan.bookedMask & ~BandMask) == 0
            && (plan.scoutedPosterMask & ~PosterMask) == 0
            && (plan.bookedMask & ~plan.attemptedMask) == 0 && Count(plan.bookedMask) <= BookingLimit
            && Count(plan.listenedMask) + Count(plan.attemptedMask) + Count(plan.scoutedPosterMask) + plan.promotions <= plan.blocks;
    }

    private static bool ValidBand(int band) { return band >= 0 && band < Bands12.Length; }
    private static bool ValidPackage(int package) { return package == 0 || package == 1; }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    private static int Clamp(int value, int min, int max) { return Math.Min(max, Math.Max(min, value)); }
    private static float ClampFinite(float value, float min, float max, float fallback) { return Finite(value) ? Math.Min(max, Math.Max(min, value)) : fallback; }
    private static int CareerValue(int[] values, int band) { return values != null && band >= 0 && band < values.Length ? Clamp(values[band], 0, 100) : 0; }
    private static int[] RepairArray(int[] values)
    {
        if (values == null || values.Length != BandCount)
        {
            int[] repaired = new int[BandCount];
            if (values != null) Array.Copy(values, repaired, Math.Min(values.Length, BandCount));
            values = repaired;
        }
        for (int i = 0; i < values.Length; i++) values[i] = Clamp(values[i], 0, 100);
        return values;
    }
    private static int FirstBits(int mask, int limit)
    {
        int result = 0;
        for (int i = 0; i < 30 && limit > 0; i++) if ((mask & (1 << i)) != 0) { result |= 1 << i; limit--; }
        return result;
    }
    private static int Count(int mask)
    {
        int count = 0;
        for (uint bits = (uint)mask; bits != 0; bits &= bits - 1) count++;
        return count;
    }

    private static bool TryPosterArtists(int posterIndex, out int mask)
    {
        mask = 0;
        var posters = SimBarPosterCatalog.Entries;
        if (posters == null || posterIndex < 0 || posterIndex >= posters.Length || posterIndex >= 30) return false;
        var poster = posters[posterIndex];
        if (poster == null || poster.artistIndices == null || poster.artistIndices.Length == 0) return false;
        foreach (int band in poster.artistIndices)
        {
            if (!ValidBand(band) || band < LegacyBandCount || !Bands12[band].isReal) { mask = 0; return false; }
            mask |= 1 << band;
        }
        return mask != 0;
    }
    private static int Roll(int day, int venue, int band, int package, int attempt)
    {
        // Explicit integer arithmetic is stable across Mono/IL2CPP, reloads and process launches.
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)day) * 16777619u;
            hash = (hash ^ (uint)venue) * 16777619u;
            hash = (hash ^ (uint)band) * 16777619u;
            hash = (hash ^ (uint)package) * 16777619u;
            hash = (hash ^ (uint)attempt) * 16777619u;
            hash ^= hash >> 16; hash *= 2246822519u; hash ^= hash >> 13;
            return (int)(hash % 100u);
        }
    }
    private static Result Fail(string code, int band = -1) { return new Result { code = code, bandIndex = band }; }
    private static Result Ok(string code, int band = -1) { return new Result { success = true, code = code, bandIndex = band }; }
}
