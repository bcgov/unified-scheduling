using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Unified.Common.Seeding;
using Unified.Db;
using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

/// <summary>
/// Seeds SubCategoryMetric rows, linking each SubCategory to its applicable metrics.
/// IDs 1-365 are reserved for seeded data; the identity sequence starts at 500.
///
/// Metric IDs reference:
///   Hours   1-23  | Count  24-42  | km  43-46  | $  47  | received/concluded  48-49
/// SubCategory IDs reference:
///   Court Security NS 1-17 | Travel NS 18 | Coroner Jury 19 | Crim/Civil Jury 20
///   Docs Civ/Fam NS 21-24  | Docs Crim NS 25-28
///   Escorts Air NS 29      | Escorts Ground NS 30
///   Escorts females 31-33  | Escorts males 34-36
///   Holding NS 37-43       | Other NS 44-49 | PIO/SIO NS 50 | Training NS 51-52
///   Court Security SUP 53-69 | Travel SUP 70
///   Docs Civ/Fam SUP 71-74  | Docs Crim SUP 75-78
///   Escorts Air SUP 79 | Escorts Ground SUP 80
///   Holding SUP 81 | Jury Admin SUP 82 | Other SUP 83-88 | PIO/SIO SUP 89 | Training SUP 90-91
/// </summary>
public class SubCategoryMetricSeeder(ILogger<SubCategoryMetricSeeder> logger) : SeederBase<UnifiedDbContext>(logger)
{
    public override int Order => 14;

    public override string Name => "SubCategoryMetric";

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

        // ── Escorts Air NS (SubCategory 29 – Security level and hours) ───────────
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

        // ── Escorts Ground NS (SubCategory 30 – Security level and hours) ────────
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

        // ── Escorts females – Escorted (SubCategories 31-33) ─────────────────────
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

        // ── Escorts males – Escorted (SubCategories 34-36) ───────────────────────
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

        // ── Escorts Air SUP (SubCategory 79 – Hours Level 1, 2, 3) ───────────────
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

        // ── Escorts Ground SUP (SubCategory 80 – Hours Level 1, 2, 3) ────────────
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

        return [.. list];
    }

    protected override async Task ExecuteAsync(UnifiedDbContext dbContext, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Updating sub-category metrics...");

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
