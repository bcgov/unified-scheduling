using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

/// <summary>
/// Seeds SubCategoryMetric rows, linking each SubCategory to its applicable metrics.
/// IDs 1-365 are reserved for seeded data (NS + SUP); IDs 366+ are Location Level (GroupId 3).
/// The identity sequence starts at 500.
///
/// Metric IDs reference:
///   Hours   1-23  | Count  24-42  | km  43-46  | $  47  | received/concluded  48-49
/// SubCategory IDs reference:
///   Court Security NS 1-17 | Travel NS 18 | Coroner Jury 19 | Crim/Civil Jury 20
///   Docs Civ/Fam NS 21-24  | Docs Crim NS 25-28
///   Transports Air NS 29      | Transports Ground NS 30
///   Transports Females 31-33  | Transports Males 34-36
///   Holding NS 37-43       | Other NS 44-49 | PIO/SIO NS 50 | Training NS 51-52
///   Court Security SUP 53-69 | Travel SUP 70
///   Docs Civ/Fam SUP 71-74  | Docs Crim SUP 75-78
///   Transports Air SUP 79 | Transports Ground SUP 80
///   Holding SUP 81 | Jury Admin SUP 82 | Other SUP 83-88 | PIO/SIO SUP 89 | Training SUP 90-91
///   Location Level: Travel 92 | Air 93 | Ground 94 | Holding 95-101 | Coroner 102 | Jury 103
/// </summary>
public class SubCategoryMetricSeeder(ILogger<SubCategoryMetricSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 14;

    public override string Name => "SubCategoryMetric";

    /// <summary>
    /// SubCategoryMetric IDs that have been moved from employee-level forms (NS/SUP)
    /// to the Location Level form (GroupId 3). These are removed from the seed set and
    /// deleted from the database during seeding.
    /// </summary>
    private static readonly HashSet<int> MovedToLocationLevelIds =
    [
        // Circuit travel NS (SubCat 18) → km Travelled
        71,
        // Coroner Jury Admin NS (SubCat 19) → Jurors Summonsed, Panels Created
        73,
        74,
        // Criminal/Civil Jury Admin NS (SubCat 20) → Summonsed, Paid, Panels, $
        76,
        77,
        78,
        79,
        // Transports Air NS (SubCat 29) → L1/L2/L3 Trips
        118,
        119,
        120,
        // Transports Ground NS (SubCat 30) → Trips, km, ground counts
        130,
        131,
        132,
        133,
        134,
        135,
        136,
        137,
        138,
        139,
        140,
        // Holding NS SubCat 37 (Adult females) → Cell Block Hours, Regulars, SEG
        165,
        167,
        168,
        // Holding NS SubCat 38 (Adult males) → Cell Block Hours, Regulars, SEG
        169,
        171,
        172,
        // Holding NS SubCat 39 (Federal Females) → Cell Block Hours, Regulars, SEG
        173,
        175,
        176,
        // Holding NS SubCat 40 (Federal Males) → Cell Block Hours, Regulars, SEG
        177,
        179,
        180,
        // Holding NS SubCat 41 (Hours) → Cell Block Hours
        181,
        // Holding NS SubCat 42 (Youth females) → Cell Block Hours, Regulars
        183,
        185,
        // Holding NS SubCat 43 (Youth males) → Cell Block Hours, Regulars
        186,
        188,
        // Circuit travel SUP (SubCat 70) → km Travelled
        283,
        // Transports Air SUP (SubCat 79) → L1/L2/L3 Trips
        322,
        323,
        324,
        // Transports Ground SUP (SubCat 80) → Trips, km
        331,
        332,
        333,
        334,
        335,
        336,
        337,
        338,
        // Holding SUP (SubCat 81) → Cell Block Hours
        339,
    ];

    private static readonly SubCategoryMetric[] SeedSubCategoryMetrics = BuildSeedData();

    private static SubCategoryMetric[] BuildSeedData()
    {
        var list = new List<SubCategoryMetric>();
        var id = 1;

        // ── Court Security NS (SubCategories 1-17) ──────────────────────────────
        // Each gets: Regular Security Staff Hours(3), Overtime Regular Security(4),
        //            High Security Staff Hours(5), Overtime High Security(6)
        for (var scId = 1; scId <= 17; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 3,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 4,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 5,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 6,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 1-68

        // ── Circuit court related travel NS (SubCategory 18 – General) ──────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 18,
                MetricId = 1,
                DisplayOrder = 1,
            }
        ); // Staff Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 18,
                MetricId = 2,
                DisplayOrder = 2,
            }
        ); // Overtime Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 18,
                MetricId = 43,
                DisplayOrder = 3,
            }
        ); // km Travelled
        // IDs 69-71

        // ── Coroner Jury Administration (SubCategory 19 – General) ──────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 19,
                MetricId = 17,
                DisplayOrder = 1,
            }
        ); // Coroner Jury Admin Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 19,
                MetricId = 27,
                DisplayOrder = 2,
            }
        ); // Coroner Jurors Summonsed
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 19,
                MetricId = 28,
                DisplayOrder = 3,
            }
        ); // Coroner Panels Created
        // IDs 72-74

        // ── Criminal/Civil Jury Administration (SubCategory 20 – General) ───────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 20,
                MetricId = 16,
                DisplayOrder = 1,
            }
        ); // Jury Admin Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 20,
                MetricId = 24,
                DisplayOrder = 2,
            }
        ); // Jurors Summonsed
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 20,
                MetricId = 25,
                DisplayOrder = 3,
            }
        ); // Jurors and Alternates Paid
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 20,
                MetricId = 26,
                DisplayOrder = 4,
            }
        ); // Panels Created
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 20,
                MetricId = 47,
                DisplayOrder = 5,
            }
        ); // Sum Total ($) Paid
        // IDs 75-79

        // ── Documents Civil/Family NS (SubCategories 21-24) ─────────────────────
        for (var scId = 21; scId <= 24; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            ); // Staff Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            ); // Overtime Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 48,
                    DisplayOrder = 3,
                }
            ); // Received
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 49,
                    DisplayOrder = 4,
                }
            ); // Concluded
        }
        // IDs 80-95

        // ── Documents Criminal NS (SubCategories 25-28) ─────────────────────────
        for (var scId = 25; scId <= 28; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 48,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 49,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 96-111

        // ── Transports Air NS (SubCategory 29 – Security level and hours) ───────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 18,
                DisplayOrder = 1,
            }
        ); // Level 1 Staff Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 19,
                DisplayOrder = 2,
            }
        ); // Level 1 Overtime Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 20,
                DisplayOrder = 3,
            }
        ); // Level 2 Staff Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 21,
                DisplayOrder = 4,
            }
        ); // Level 2 Overtime Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 22,
                DisplayOrder = 5,
            }
        ); // Level 3 Staff Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 23,
                DisplayOrder = 6,
            }
        ); // Level 3 Overtime Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 30,
                DisplayOrder = 7,
            }
        ); // Level 1 Number of Trips
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 31,
                DisplayOrder = 8,
            }
        ); // Level 2 Number of Trips
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 32,
                DisplayOrder = 9,
            }
        ); // Level 3 Number of Trips
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 36,
                DisplayOrder = 10,
            }
        ); // Level 1 Air (count)
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 38,
                DisplayOrder = 11,
            }
        ); // Level 2 Air (count)
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 29,
                MetricId = 40,
                DisplayOrder = 12,
            }
        ); // Level 3 Air (count)
        // IDs 112-123

        // ── Transports Ground NS (SubCategory 30 – Security level and hours) ────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 18,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 19,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 20,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 21,
                DisplayOrder = 4,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 22,
                DisplayOrder = 5,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 23,
                DisplayOrder = 6,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 29,
                DisplayOrder = 7,
            }
        ); // Number of Trips
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 30,
                DisplayOrder = 8,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 31,
                DisplayOrder = 9,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 32,
                DisplayOrder = 10,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 43,
                DisplayOrder = 11,
            }
        ); // km Travelled
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 44,
                DisplayOrder = 12,
            }
        ); // Level 1 Ground km
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 45,
                DisplayOrder = 13,
            }
        ); // Level 2 Ground km
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 46,
                DisplayOrder = 14,
            }
        ); // Level 3 Ground km
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 37,
                DisplayOrder = 15,
            }
        ); // Level 1 Ground (count)
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 39,
                DisplayOrder = 16,
            }
        ); // Level 2 Ground (count)
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 30,
                MetricId = 41,
                DisplayOrder = 17,
            }
        ); // Level 3 Ground (count)
        // IDs 124-140

        // ── Transports Females – Transported (SubCategories 31-33) ─────────────────────
        // 31=adult females, 32=Federal females, 33=youth females
        for (var scId = 31; scId <= 33; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            ); // Staff Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            ); // Overtime Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 33,
                    DisplayOrder = 3,
                }
            ); // Custodies – Regulars
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 34,
                    DisplayOrder = 4,
                }
            ); // Custodies – SEG/PC/MH
        }
        // IDs 141-152

        // ── Transports Males – Transported (SubCategories 34-36) ───────────────────────
        for (var scId = 34; scId <= 36; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 33,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 34,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 153-164

        // ── Holding area/cellblock NS (SubCategories 37-43) ──────────────────────
        // 37=Adult females Prov, 38=Adult males Prov, 39=Federal Females, 40=Federal Males
        for (var scId = 37; scId <= 40; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 14,
                    DisplayOrder = 1,
                }
            ); // Cell Block Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 15,
                    DisplayOrder = 2,
                }
            ); // Overtime Staff Hours
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 33,
                    DisplayOrder = 3,
                }
            ); // Custodies – Regulars
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 34,
                    DisplayOrder = 4,
                }
            ); // Custodies – SEG/PC/MH
        }
        // 41=Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 41,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 41,
                MetricId = 15,
                DisplayOrder = 2,
            }
        );
        // 42=Youth females Prov, 43=Youth males Prov
        for (var scId = 42; scId <= 43; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 14,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 15,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 33,
                    DisplayOrder = 3,
                }
            );
        }
        // IDs 165-188

        // ── Other NS (SubCategories 44-49) ────────────────────────────────────────
        // 44=Administration/other duties
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 44,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 44,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // 45=CPIC checks for Jury Administration
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 45,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 45,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // 46=CPIC/JUSTIN
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 46,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 46,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 46,
                MetricId = 48,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 46,
                MetricId = 49,
                DisplayOrder = 4,
            }
        );
        // 47=Completion of Incident (SIR)
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 47,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 47,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // 48=DNA samples
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 48,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 48,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 48,
                MetricId = 35,
                DisplayOrder = 3,
            }
        ); // Number of Samples Taken
        // 49=Vehicle Management
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 49,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 49,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // IDs 189-203

        // ── PIO/SIO NS (SubCategory 50 – General) ────────────────────────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 50,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 50,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // IDs 204-205

        // ── Training NS (SubCategories 51-52) ─────────────────────────────────────
        // 51=Instruction
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 51,
                MetricId = 7,
                DisplayOrder = 1,
            }
        ); // Instructor Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 51,
                MetricId = 8,
                DisplayOrder = 2,
            }
        ); // Instructor Overtime
        // 52=Student
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 52,
                MetricId = 9,
                DisplayOrder = 1,
            }
        ); // PTO Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 52,
                MetricId = 10,
                DisplayOrder = 2,
            }
        ); // PTO Overtime
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 52,
                MetricId = 11,
                DisplayOrder = 3,
            }
        ); // Branch Directed Hours
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 52,
                MetricId = 12,
                DisplayOrder = 4,
            }
        ); // Branch Directed Overtime
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 52,
                MetricId = 13,
                DisplayOrder = 5,
            }
        ); // Self Development Hours
        // IDs 206-212

        // ── Court Security SUP (SubCategories 53-69) ─────────────────────────────
        for (var scId = 53; scId <= 69; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 3,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 4,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 5,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 6,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 213-280

        // ── Circuit court related travel SUP (SubCategory 70 – Hours) ────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 70,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 70,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 70,
                MetricId = 43,
                DisplayOrder = 3,
            }
        );
        // IDs 281-283

        // ── Documents Civil/Family SUP (SubCategories 71-74) ──────────────────────
        for (var scId = 71; scId <= 74; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 48,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 49,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 284-299

        // ── Documents Criminal SUP (SubCategories 75-78) ──────────────────────────
        for (var scId = 75; scId <= 78; scId++)
        {
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 1,
                    DisplayOrder = 1,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 2,
                    DisplayOrder = 2,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 48,
                    DisplayOrder = 3,
                }
            );
            list.Add(
                new()
                {
                    Id = id++,
                    SubCategoryId = scId,
                    MetricId = 49,
                    DisplayOrder = 4,
                }
            );
        }
        // IDs 300-315

        // ── Transports Air SUP (SubCategory 79 – Hours Level 1, 2, 3) ───────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 18,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 19,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 20,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 21,
                DisplayOrder = 4,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 22,
                DisplayOrder = 5,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 23,
                DisplayOrder = 6,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 30,
                DisplayOrder = 7,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 31,
                DisplayOrder = 8,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 79,
                MetricId = 32,
                DisplayOrder = 9,
            }
        );
        // IDs 316-324

        // ── Transports Ground SUP (SubCategory 80 – Hours Level 1, 2, 3) ────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 18,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 19,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 20,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 21,
                DisplayOrder = 4,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 22,
                DisplayOrder = 5,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 23,
                DisplayOrder = 6,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 29,
                DisplayOrder = 7,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 30,
                DisplayOrder = 8,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 31,
                DisplayOrder = 9,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 32,
                DisplayOrder = 10,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 43,
                DisplayOrder = 11,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 44,
                DisplayOrder = 12,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 45,
                DisplayOrder = 13,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 80,
                MetricId = 46,
                DisplayOrder = 14,
            }
        );
        // IDs 325-338

        // ── Holding area/cellblock SUP (SubCategory 81 – Hours) ──────────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 81,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 81,
                MetricId = 15,
                DisplayOrder = 2,
            }
        );
        // IDs 339-340

        // ── Jury Administration SUP (SubCategory 82 – Hours) ─────────────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 82,
                MetricId = 16,
                DisplayOrder = 1,
            }
        ); // Jury Admin Hours
        // ID 341

        // ── Other SUP (SubCategories 83-88) ──────────────────────────────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 83,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 83,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 84,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 84,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 85,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 85,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 85,
                MetricId = 48,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 85,
                MetricId = 49,
                DisplayOrder = 4,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 86,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 86,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 87,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 87,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 87,
                MetricId = 35,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 88,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 88,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // IDs 342-356

        // ── PIO/SIO SUP (SubCategory 89 – General) ────────────────────────────────
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 89,
                MetricId = 1,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 89,
                MetricId = 2,
                DisplayOrder = 2,
            }
        );
        // IDs 357-358

        // ── Training SUP (SubCategories 90-91) ────────────────────────────────────
        // 90=Instruction
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 90,
                MetricId = 7,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 90,
                MetricId = 8,
                DisplayOrder = 2,
            }
        );
        // 91=Student
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 91,
                MetricId = 9,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 91,
                MetricId = 10,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 91,
                MetricId = 11,
                DisplayOrder = 3,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 91,
                MetricId = 12,
                DisplayOrder = 4,
            }
        );
        list.Add(
            new()
            {
                Id = id++,
                SubCategoryId = 91,
                MetricId = 13,
                DisplayOrder = 5,
            }
        );
        // IDs 359-365

        // Remove entries that have been moved to Location Level (GroupId 3).
        // The original IDs are preserved so that remaining entries keep stable IDs.
        list.RemoveAll(scm => MovedToLocationLevelIds.Contains(scm.Id));

        // ── Location Level (GroupId 3) ─────────────────────────────────────────

        // Circuit court related travel LL (SubCategory 92 – General)
        list.Add(
            new()
            {
                Id = 366,
                SubCategoryId = 92,
                MetricId = 43,
                DisplayOrder = 1,
            }
        ); // km Travelled

        // Transports Air LL (SubCategory 93 – General)
        list.Add(
            new()
            {
                Id = 367,
                SubCategoryId = 93,
                MetricId = 30,
                DisplayOrder = 1,
            }
        ); // L1 Trips
        list.Add(
            new()
            {
                Id = 368,
                SubCategoryId = 93,
                MetricId = 31,
                DisplayOrder = 2,
            }
        ); // L2 Trips
        list.Add(
            new()
            {
                Id = 369,
                SubCategoryId = 93,
                MetricId = 32,
                DisplayOrder = 3,
            }
        ); // L3 Trips

        // Transports Ground LL (SubCategory 94 – General)
        list.Add(
            new()
            {
                Id = 370,
                SubCategoryId = 94,
                MetricId = 29,
                DisplayOrder = 1,
            }
        ); // Trips
        list.Add(
            new()
            {
                Id = 371,
                SubCategoryId = 94,
                MetricId = 30,
                DisplayOrder = 2,
            }
        ); // L1 Trips
        list.Add(
            new()
            {
                Id = 372,
                SubCategoryId = 94,
                MetricId = 31,
                DisplayOrder = 3,
            }
        ); // L2 Trips
        list.Add(
            new()
            {
                Id = 373,
                SubCategoryId = 94,
                MetricId = 32,
                DisplayOrder = 4,
            }
        ); // L3 Trips
        list.Add(
            new()
            {
                Id = 374,
                SubCategoryId = 94,
                MetricId = 43,
                DisplayOrder = 5,
            }
        ); // km
        list.Add(
            new()
            {
                Id = 375,
                SubCategoryId = 94,
                MetricId = 44,
                DisplayOrder = 6,
            }
        ); // L1 Ground km
        list.Add(
            new()
            {
                Id = 376,
                SubCategoryId = 94,
                MetricId = 45,
                DisplayOrder = 7,
            }
        ); // L2 Ground km
        list.Add(
            new()
            {
                Id = 377,
                SubCategoryId = 94,
                MetricId = 46,
                DisplayOrder = 8,
            }
        ); // L3 Ground km
        list.Add(
            new()
            {
                Id = 378,
                SubCategoryId = 94,
                MetricId = 37,
                DisplayOrder = 9,
            }
        ); // L1 Ground count
        list.Add(
            new()
            {
                Id = 379,
                SubCategoryId = 94,
                MetricId = 39,
                DisplayOrder = 10,
            }
        ); // L2 Ground count
        list.Add(
            new()
            {
                Id = 380,
                SubCategoryId = 94,
                MetricId = 41,
                DisplayOrder = 11,
            }
        ); // L3 Ground count

        // Holding area/cellblock LL (SubCategories 95-101)
        // 95=Adult females Prov
        list.Add(
            new()
            {
                Id = 381,
                SubCategoryId = 95,
                MetricId = 14,
                DisplayOrder = 1,
            }
        ); // Cell Block Hours
        list.Add(
            new()
            {
                Id = 382,
                SubCategoryId = 95,
                MetricId = 33,
                DisplayOrder = 2,
            }
        ); // Regulars
        list.Add(
            new()
            {
                Id = 383,
                SubCategoryId = 95,
                MetricId = 34,
                DisplayOrder = 3,
            }
        ); // SEG
        // 96=Adult males Prov
        list.Add(
            new()
            {
                Id = 384,
                SubCategoryId = 96,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = 385,
                SubCategoryId = 96,
                MetricId = 33,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = 386,
                SubCategoryId = 96,
                MetricId = 34,
                DisplayOrder = 3,
            }
        );
        // 97=Federal Females
        list.Add(
            new()
            {
                Id = 387,
                SubCategoryId = 97,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = 388,
                SubCategoryId = 97,
                MetricId = 33,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = 389,
                SubCategoryId = 97,
                MetricId = 34,
                DisplayOrder = 3,
            }
        );
        // 98=Federal Males
        list.Add(
            new()
            {
                Id = 390,
                SubCategoryId = 98,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = 391,
                SubCategoryId = 98,
                MetricId = 33,
                DisplayOrder = 2,
            }
        );
        list.Add(
            new()
            {
                Id = 392,
                SubCategoryId = 98,
                MetricId = 34,
                DisplayOrder = 3,
            }
        );
        // 99=Hours
        list.Add(
            new()
            {
                Id = 393,
                SubCategoryId = 99,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        // 100=Youth females Prov
        list.Add(
            new()
            {
                Id = 394,
                SubCategoryId = 100,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = 395,
                SubCategoryId = 100,
                MetricId = 33,
                DisplayOrder = 2,
            }
        );
        // 101=Youth males Prov
        list.Add(
            new()
            {
                Id = 396,
                SubCategoryId = 101,
                MetricId = 14,
                DisplayOrder = 1,
            }
        );
        list.Add(
            new()
            {
                Id = 397,
                SubCategoryId = 101,
                MetricId = 33,
                DisplayOrder = 2,
            }
        );

        // Coroner Jury Administration LL (SubCategory 102 – General)
        list.Add(
            new()
            {
                Id = 398,
                SubCategoryId = 102,
                MetricId = 27,
                DisplayOrder = 1,
            }
        ); // Jurors Summonsed
        list.Add(
            new()
            {
                Id = 399,
                SubCategoryId = 102,
                MetricId = 28,
                DisplayOrder = 2,
            }
        ); // Panels Created

        // Criminal/Civil Jury Administration LL (SubCategory 103 – General)
        list.Add(
            new()
            {
                Id = 400,
                SubCategoryId = 103,
                MetricId = 24,
                DisplayOrder = 1,
            }
        ); // Jurors Summonsed
        list.Add(
            new()
            {
                Id = 401,
                SubCategoryId = 103,
                MetricId = 25,
                DisplayOrder = 2,
            }
        ); // Jurors Paid
        list.Add(
            new()
            {
                Id = 402,
                SubCategoryId = 103,
                MetricId = 26,
                DisplayOrder = 3,
            }
        ); // Panels Created
        list.Add(
            new()
            {
                Id = 403,
                SubCategoryId = 103,
                MetricId = 47,
                DisplayOrder = 4,
            }
        ); // Sum Total ($)
        // IDs 366-403

        return [.. list];
    }

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating sub-category metrics...");

        // Delete entries that have been moved to Location Level (GroupId 3).
        var movedRecords = await dbContext
            .SubCategoryMetrics.Where(scm => MovedToLocationLevelIds.Contains(scm.Id))
            .ToListAsync(cancellationToken);
        if (movedRecords.Count > 0)
        {
            dbContext.SubCategoryMetrics.RemoveRange(movedRecords);
            await dbContext.SaveChangesAsync(cancellationToken);
            Logger.LogInformation(
                "Removed {Count} SubCategoryMetric entries moved to Location Level.",
                movedRecords.Count
            );
        }

        var createdCount = 0;
        var updatedCount = 0;

        foreach (var seed in SeedSubCategoryMetrics)
        {
            var existing = await dbContext.SubCategoryMetrics.FirstOrDefaultAsync(
                scm => scm.Id == seed.Id,
                cancellationToken
            );

            if (existing is null)
            {
                Logger.LogInformation("SubCategoryMetric with Id {Id} does not exist, adding it...", seed.Id);
                await dbContext.SubCategoryMetrics.AddAsync(seed, cancellationToken);
                createdCount++;
                continue;
            }

            Logger.LogInformation("Updating fields for SubCategoryMetric with Id {Id}...", seed.Id);
            existing.SubCategoryId = seed.SubCategoryId;
            existing.MetricId = seed.MetricId;
            existing.DisplayOrder = seed.DisplayOrder;
            updatedCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        Logger.LogInformation(
            "SubCategoryMetric seeding complete. Created {CreatedCount}, updated {UpdatedCount}.",
            createdCount,
            updatedCount
        );
    }
}
