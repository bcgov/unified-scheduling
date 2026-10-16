using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public static class BiOracleStatMappingSeedData
{
    public static IReadOnlyList<BiOracleStatMappingSetSeedDefinition> Definitions { get; } =
    [new() { Id = 1, Mappings = CreateMappings() }];

    private static IReadOnlyList<BiOracleStatMappingSeedDefinition> CreateMappings()
    {
        var mappings = new List<BiOracleStatMappingSeedDefinition>();

        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CA_REG_HRS", 5, 6, 217, 218);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_REG_REG_HRS", 5, 217);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_REG_OT_HRS", 6, 218);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CA_HSM_HRS", 7, 8, 219, 220);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_HSM_REG_HRS", 7, 219);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_HSM_OT_HRS", 8, 220);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_JY_SHRF_HRS", 37, 38, 249, 250);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_SHRF_REG_HRS", 37, 249);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_SHRF_OT_HRS", 38, 250);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_JY_HSM_HRS", 39, 40, 251, 252);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_HSM_REG_HRS", 39, 251);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_HSM_OT_HRS", 40, 252);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_DELIB_HRS", 41, 42, 253, 254);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_REG_HRS", 41, 253);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_OT_HRS", 42, 254);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_DELIB_HSM_HRS", 43, 44, 255, 256);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_HSM_REG_HRS", 43, 255);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_HSM_OT_HRS", 44, 256);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_NJ_SHRF_HRS", 45, 46, 257, 258);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_SHRF_REG_HRS", 45, 257);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_SHRF_OT_HRS", 46, 258);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCV_NJ_HSM_HRS", 47, 48, 259, 260);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_HSM_REG_HRS", 47, 259);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_HSM_OT_HRS", 48, 260);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_JY_SHRF_HRS", 49, 50, 261, 262);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_SHRF_REG_HRS", 49, 261);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_SHRF_OT_HRS", 50, 262);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_JY_HSM_HRS", 51, 52, 263, 264);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_HSM_REG_HRS", 51, 263);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_HSM_OT_HRS", 52, 264);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_DELIB_HRS", 53, 54, 265, 266);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_REG_HRS", 53, 265);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_OT_HRS", 54, 266);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_DELIB_HSM_HRS", 55, 56, 267, 268);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_HSM_REG_HRS", 55, 267);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_HSM_OT_HRS", 56, 268);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_NJ_SHRF_HRS", 57, 58, 269, 270);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_SHRF_REG_HRS", 57, 269);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_SHRF_OT_HRS", 58, 270);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCR_NJ_HSM_HRS", 59, 60, 271, 272);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_HSM_REG_HRS", 59, 271);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_HSM_OT_HRS", 60, 272);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCVF_SHRF_HRS", 61, 62, 273, 274);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_SHRF_REG_HRS", 61, 273);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_SHRF_OT_HRS", 62, 274);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SCVF_HSM_HRS", 63, 64, 275, 276);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_HSM_REG_HRS", 63, 275);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_HSM_OT_HRS", 64, 276);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCVF_SHRF_HRS", 17, 18, 229, 230);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_SHRF_REG_HRS", 17, 229);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_SHRF_OT_HRS", 18, 230);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCVF_HSM_HRS", 19, 20, 231, 232);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_HSM_REG_HRS", 19, 231);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_HSM_OT_HRS", 20, 232);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCVS_SHRF_HRS", 21, 22, 233, 234);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_SHRF_REG_HRS", 21, 233);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_SHRF_OT_HRS", 22, 234);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCVS_HSM_HRS", 23, 24, 235, 236);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_HSM_REG_HRS", 23, 235);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_HSM_OT_HRS", 24, 236);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCRA_SHRF_HRS", 13, 14, 225, 226);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_SHRF_REG_HRS", 13, 225);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_SHRF_OT_HRS", 14, 226);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCRA_HSM_HRS", 15, 16, 227, 228);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_HSM_REG_HRS", 15, 227);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_HSM_OT_HRS", 16, 228);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCRY_SHRF_HRS", 25, 26, 237, 238);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_SHRF_REG_HRS", 25, 237);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_SHRF_OT_HRS", 26, 238);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PCRY_HSM_HRS", 27, 28, 239, 240);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_HSM_REG_HRS", 27, 239);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_HSM_OT_HRS", 28, 240);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "OTHR_SHRF_HRS", 9, 10, 221, 222);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_SHRF_REG_HRS", 9, 221);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_SHRF_OT_HRS", 10, 222);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "OTHR_HSM_HRS", 11, 12, 223, 224);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_HSM_REG_HRS", 11, 223);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_HSM_OT_HRS", 12, 224);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "RVR_SHRF_HRS", 29, 30, 241, 242);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_SHRF_REG_HRS", 29, 241);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_SHRF_OT_HRS", 30, 242);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "RVR_HSM_HRS", 31, 32, 243, 244);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_HSM_REG_HRS", 31, 243);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_HSM_OT_HRS", 32, 244);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SG_STAFF_HRS", 33, 34, 245, 246);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_STAFF_REG_HRS", 33, 245);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_STAFF_OT_HRS", 34, 246);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SG_HSM_HRS", 35, 36, 247, 248);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_HSM_REG_HRS", 35, 247);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_HSM_OT_HRS", 36, 248);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRNR_SHRF_HRS", 1, 2, 213, 214);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_SHRF_REG_HRS", 1, 213);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_SHRF_OT_HRS", 2, 214);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRNR_HSM_HRS", 3, 4, 215, 216);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_HSM_REG_HRS", 3, 215);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_HSM_OT_HRS", 4, 216);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "VC_STAFF_HRS", 65, 66, 277, 278);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_STAFF_REG_HRS", 65, 277);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_STAFF_OT_HRS", 66, 278);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "VC_HSM_HRS", 67, 68, 279, 280);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_HSM_REG_HRS", 67, 279);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_HSM_OT_HRS", 68, 280);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "HLD_SHRF_HRS", 404, 405, 406, 340);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "HLD_SHRF_REG_HRS", 404, 406);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "HLD_SHRF_OT_HRS", 405, 340);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L1", 124, 125, 325, 326);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1", 124, 325);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L1", 125, 326);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L2", 126, 127, 327, 328);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L2", 126, 327);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L2", 127, 328);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_HRS_L3", 128, 129, 329, 330);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L3", 128, 329);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L3", 129, 330);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_HRS_L1", 112, 113, 316, 317);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L1", 112, 316);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L1", 113, 317);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_HRS_L2", 114, 115, 318, 319);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L2", 114, 318);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L2", 115, 319);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_HRS_L3", 116, 117, 320, 321);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L3", 116, 320);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L3", 117, 321);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CCRT_HRS", 69, 70, 281, 282);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CCRT_REG_HRS", 69, 281);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CCRT_OT_HRS", 70, 282);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JRY_SHRF_HRS", 75, 341);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_ADMN_HRS", 72, 428);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_WARR_HRS", 92, 93, 296, 297);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_WARR_REG_HRS", 92, 296);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_WARR_OT_HRS", 93, 297);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_CRTORD_HRS", 80, 81, 284, 285);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_CRTORD_REG_HRS", 80, 284);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_CRTORD_OT_HRS", 81, 285);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_DOCS_HRS", 84, 85, 288, 289);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_DOCS_REG_HRS", 84, 288);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_DOCS_OT_HRS", 85, 289);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_OTHR_HRS", 88, 89, 292, 293);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_OTHR_REG_HRS", 88, 292);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_OTHR_OT_HRS", 89, 293);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_WARR_HRS", 108, 109, 312, 313);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_WARR_REG_HRS", 108, 312);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_WARR_OT_HRS", 109, 313);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_CRTORD_HRS", 96, 97, 300, 301);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_CRTORD_REG_HRS", 96, 300);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_CRTORD_OT_HRS", 97, 301);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_DOCS_HRS", 100, 101, 304, 305);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_DOCS_REG_HRS", 100, 304);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_DOCS_OT_HRS", 101, 305);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_OTHR_HRS", 104, 105, 308, 309);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_OTHR_REG_HRS", 104, 308);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_OTHR_OT_HRS", 105, 309);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PTO_HRS", 429, 430, 431, 432);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PTO_REG_HRS", 429, 431);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PTO_OT_HRS", 430, 432);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "INO_HRS", 206, 207, 359, 360);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "INO_REG_HRS", 206, 359);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "INO_OT_HRS", 207, 360);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "BRDI_HRS", 210, 211, 363, 364);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "BRDI_REG_HRS", 210, 363);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "BRDI_OT_HRS", 211, 364);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SEDEV_HRS", 212, 365);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "DNA_HRS", 199, 200, 352, 353);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "DNA_REG_HRS", 199, 352);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "DNA_OT_HRS", 200, 353);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "DNA_SAMPLES_CNT", 201, 354);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "ADMIN_HRS", 189, 190, 342, 343);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "ADMIN_REG_HRS", 189, 342);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "ADMIN_OT_HRS", 190, 343);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CPIC_JUSTIN_HRS", 193, 194, 346, 347);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JUSTIN_REG_HRS", 193, 346);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JUSTIN_OT_HRS", 194, 347);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CPIC_JA_HRS", 191, 192, 344, 345);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JA_REG_HRS", 191, 344);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JA_OT_HRS", 192, 345);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SIR_HRS", 197, 198, 350, 351);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SIR_REG_HRS", 197, 350);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SIR_OT_HRS", 198, 351);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "VEH_MGNT_HRS", 202, 203, 355, 356);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VEH_MGNT_REG_HRS", 202, 355);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VEH_MGNT_OT_HRS", 203, 356);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "PIO_IO_HRS", 204, 205, 357, 358);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PIO_IO_REG_HRS", 204, 357);
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PIO_IO_OT_HRS", 205, 358);

        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CCRT_KMS", 366);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L1", 367);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L2", 368);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L3", 369);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L1", 371);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L2", 372);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L3", 373);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L1", 375);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L2", 376);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L3", 377);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_KM_TRAVL", 375, 376, 377);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_JRS_SMND", 398);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_PNLS", 399);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_SUMD", 400);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PAID", 401);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PANELS_CNT", 402);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PAID_SUM", 403);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "HLD_CELL_HRS", 407);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_CRTORD_RCVD", 433);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_CRTORD_CONC", 434);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_WARR_RCVD", 439);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_WARR_CONC", 440);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_CRTORD_RCVD", 441);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_CRTORD_CONC", 442);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_WARR_RCVD", 447);
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_WARR_CONC", 448);

        // SCMs 435/436/443/444 need a business decision: legacy *_DOCS_*, *_OTHR_*, or both.

        return mappings;
    }

    private static void AddMappings(
        ICollection<BiOracleStatMappingSeedDefinition> mappings,
        string targetTable,
        string targetColumn,
        params int[] subCategoryMetricIds
    )
    {
        foreach (var subCategoryMetricId in subCategoryMetricIds)
        {
            mappings.Add(
                new BiOracleStatMappingSeedDefinition
                {
                    Id = mappings.Count + 1,
                    SubCategoryMetricId = subCategoryMetricId,
                    TargetTable = targetTable,
                    TargetColumn = targetColumn,
                }
            );
        }
    }
}
