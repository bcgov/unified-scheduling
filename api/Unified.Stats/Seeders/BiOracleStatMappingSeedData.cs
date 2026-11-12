using Unified.Db.Models.Stats;

namespace Unified.Stats.Seeders;

public static class BiOracleStatMappingSeedData
{
    public static IReadOnlyList<BiOracleStatMappingSetSeedDefinition> Definitions { get; } =
    [
        new()
        {
            Id = 1,
            EffectiveDate = new DateOnly(2026, 11, 1),
            Mappings = CreateMappings(),
        },
    ];

    private static IReadOnlyList<BiOracleStatMappingSeedDefinition> CreateMappings()
    {
        var mappings = new List<BiOracleStatMappingSeedDefinition>();

        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CA_REG_HRS", (1, 5), (2, 6), (3, 217), (4, 218));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_REG_REG_HRS", (5, 5), (6, 217));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_REG_OT_HRS", (7, 6), (8, 218));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CA_HSM_HRS", (9, 7), (10, 8), (11, 219), (12, 220));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_HSM_REG_HRS", (13, 7), (14, 219));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CA_HSM_OT_HRS", (15, 8), (16, 220));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_JY_SHRF_HRS",
            (17, 37),
            (18, 38),
            (19, 249),
            (20, 250)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_SHRF_REG_HRS", (21, 37), (22, 249));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_SHRF_OT_HRS", (23, 38), (24, 250));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_JY_HSM_HRS",
            (25, 39),
            (26, 40),
            (27, 251),
            (28, 252)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_HSM_REG_HRS", (29, 39), (30, 251));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_JY_HSM_OT_HRS", (31, 40), (32, 252));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_DELIB_HRS",
            (33, 41),
            (34, 42),
            (35, 253),
            (36, 254)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_REG_HRS", (37, 41), (38, 253));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_OT_HRS", (39, 42), (40, 254));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_DELIB_HSM_HRS",
            (41, 43),
            (42, 44),
            (43, 255),
            (44, 256)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_HSM_REG_HRS", (45, 43), (46, 255));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_DELIB_HSM_OT_HRS", (47, 44), (48, 256));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_NJ_SHRF_HRS",
            (49, 45),
            (50, 46),
            (51, 257),
            (52, 258)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_SHRF_REG_HRS", (53, 45), (54, 257));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_SHRF_OT_HRS", (55, 46), (56, 258));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCV_NJ_HSM_HRS",
            (57, 47),
            (58, 48),
            (59, 259),
            (60, 260)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_HSM_REG_HRS", (61, 47), (62, 259));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCV_NJ_HSM_OT_HRS", (63, 48), (64, 260));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_JY_SHRF_HRS",
            (65, 49),
            (66, 50),
            (67, 261),
            (68, 262)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_SHRF_REG_HRS", (69, 49), (70, 261));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_SHRF_OT_HRS", (71, 50), (72, 262));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_JY_HSM_HRS",
            (73, 51),
            (74, 52),
            (75, 263),
            (76, 264)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_HSM_REG_HRS", (77, 51), (78, 263));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_JY_HSM_OT_HRS", (79, 52), (80, 264));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_DELIB_HRS",
            (81, 53),
            (82, 54),
            (83, 265),
            (84, 266)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_REG_HRS", (85, 53), (86, 265));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_OT_HRS", (87, 54), (88, 266));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_DELIB_HSM_HRS",
            (89, 55),
            (90, 56),
            (91, 267),
            (92, 268)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_HSM_REG_HRS", (93, 55), (94, 267));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_DELIB_HSM_OT_HRS", (95, 56), (96, 268));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_NJ_SHRF_HRS",
            (97, 57),
            (98, 58),
            (99, 269),
            (100, 270)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_SHRF_REG_HRS", (101, 57), (102, 269));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_SHRF_OT_HRS", (103, 58), (104, 270));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCR_NJ_HSM_HRS",
            (105, 59),
            (106, 60),
            (107, 271),
            (108, 272)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_HSM_REG_HRS", (109, 59), (110, 271));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCR_NJ_HSM_OT_HRS", (111, 60), (112, 272));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCVF_SHRF_HRS",
            (113, 61),
            (114, 62),
            (115, 273),
            (116, 274)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_SHRF_REG_HRS", (117, 61), (118, 273));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_SHRF_OT_HRS", (119, 62), (120, 274));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SCVF_HSM_HRS",
            (121, 63),
            (122, 64),
            (123, 275),
            (124, 276)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_HSM_REG_HRS", (125, 63), (126, 275));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SCVF_HSM_OT_HRS", (127, 64), (128, 276));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCVF_SHRF_HRS",
            (129, 17),
            (130, 18),
            (131, 229),
            (132, 230)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_SHRF_REG_HRS", (133, 17), (134, 229));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_SHRF_OT_HRS", (135, 18), (136, 230));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCVF_HSM_HRS",
            (137, 19),
            (138, 20),
            (139, 231),
            (140, 232)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_HSM_REG_HRS", (141, 19), (142, 231));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVF_HSM_OT_HRS", (143, 20), (144, 232));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCVS_SHRF_HRS",
            (145, 21),
            (146, 22),
            (147, 233),
            (148, 234)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_SHRF_REG_HRS", (149, 21), (150, 233));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_SHRF_OT_HRS", (151, 22), (152, 234));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCVS_HSM_HRS",
            (153, 23),
            (154, 24),
            (155, 235),
            (156, 236)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_HSM_REG_HRS", (157, 23), (158, 235));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCVS_HSM_OT_HRS", (159, 24), (160, 236));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCRA_SHRF_HRS",
            (161, 13),
            (162, 14),
            (163, 225),
            (164, 226)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_SHRF_REG_HRS", (165, 13), (166, 225));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_SHRF_OT_HRS", (167, 14), (168, 226));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCRA_HSM_HRS",
            (169, 15),
            (170, 16),
            (171, 227),
            (172, 228)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_HSM_REG_HRS", (173, 15), (174, 227));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRA_HSM_OT_HRS", (175, 16), (176, 228));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCRY_SHRF_HRS",
            (177, 25),
            (178, 26),
            (179, 237),
            (180, 238)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_SHRF_REG_HRS", (181, 25), (182, 237));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_SHRF_OT_HRS", (183, 26), (184, 238));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PCRY_HSM_HRS",
            (185, 27),
            (186, 28),
            (187, 239),
            (188, 240)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_HSM_REG_HRS", (189, 27), (190, 239));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PCRY_HSM_OT_HRS", (191, 28), (192, 240));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "OTHR_SHRF_HRS",
            (193, 9),
            (194, 10),
            (195, 221),
            (196, 222)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_SHRF_REG_HRS", (197, 9), (198, 221));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_SHRF_OT_HRS", (199, 10), (200, 222));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "OTHR_HSM_HRS",
            (201, 11),
            (202, 12),
            (203, 223),
            (204, 224)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_HSM_REG_HRS", (205, 11), (206, 223));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "OTHR_HSM_OT_HRS", (207, 12), (208, 224));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "RVR_SHRF_HRS",
            (209, 29),
            (210, 30),
            (211, 241),
            (212, 242)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_SHRF_REG_HRS", (213, 29), (214, 241));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_SHRF_OT_HRS", (215, 30), (216, 242));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "RVR_HSM_HRS",
            (217, 31),
            (218, 32),
            (219, 243),
            (220, 244)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_HSM_REG_HRS", (221, 31), (222, 243));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "RVR_HSM_OT_HRS", (223, 32), (224, 244));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SG_STAFF_HRS",
            (225, 33),
            (226, 34),
            (227, 245),
            (228, 246)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_STAFF_REG_HRS", (229, 33), (230, 245));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_STAFF_OT_HRS", (231, 34), (232, 246));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SG_HSM_HRS",
            (233, 35),
            (234, 36),
            (235, 247),
            (236, 248)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_HSM_REG_HRS", (237, 35), (238, 247));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SG_HSM_OT_HRS", (239, 36), (240, 248));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRNR_SHRF_HRS",
            (241, 1),
            (242, 2),
            (243, 213),
            (244, 214)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_SHRF_REG_HRS", (245, 1), (246, 213));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_SHRF_OT_HRS", (247, 2), (248, 214));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRNR_HSM_HRS",
            (249, 3),
            (250, 4),
            (251, 215),
            (252, 216)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_HSM_REG_HRS", (253, 3), (254, 215));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRNR_HSM_OT_HRS", (255, 4), (256, 216));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "VC_STAFF_HRS",
            (257, 65),
            (258, 66),
            (259, 277),
            (260, 278)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_STAFF_REG_HRS", (261, 65), (262, 277));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_STAFF_OT_HRS", (263, 66), (264, 278));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "VC_HSM_HRS",
            (265, 67),
            (266, 68),
            (267, 279),
            (268, 280)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_HSM_REG_HRS", (269, 67), (270, 279));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VC_HSM_OT_HRS", (271, 68), (272, 280));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "HLD_SHRF_HRS",
            (273, 404),
            (274, 405),
            (275, 406),
            (276, 340)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "HLD_SHRF_REG_HRS", (277, 404), (278, 406));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "HLD_SHRF_OT_HRS", (279, 405), (280, 340));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "GRD_ESC_HRS_L1",
            (281, 124),
            (282, 125),
            (283, 325),
            (284, 326)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L1", (285, 124), (286, 325));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L1", (287, 125), (288, 326));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "GRD_ESC_HRS_L2",
            (289, 126),
            (290, 127),
            (291, 327),
            (292, 328)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L2", (293, 126), (294, 327));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L2", (295, 127), (296, 328));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "GRD_ESC_HRS_L3",
            (297, 128),
            (298, 129),
            (299, 329),
            (300, 330)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_REG_HRS_L3", (301, 128), (302, 329));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "GRD_ESC_OT_HRS_L3", (303, 129), (304, 330));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "AIR_ESC_HRS_L1",
            (305, 112),
            (306, 113),
            (307, 316),
            (308, 317)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L1", (309, 112), (310, 316));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L1", (311, 113), (312, 317));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "AIR_ESC_HRS_L2",
            (313, 114),
            (314, 115),
            (315, 318),
            (316, 319)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L2", (317, 114), (318, 318));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L2", (319, 115), (320, 319));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "AIR_ESC_HRS_L3",
            (321, 116),
            (322, 117),
            (323, 320),
            (324, 321)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_REG_HRS_L3", (325, 116), (326, 320));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "AIR_ESC_OT_HRS_L3", (327, 117), (328, 321));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CCRT_HRS",
            (329, 69),
            (330, 70),
            (331, 281),
            (332, 282)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CCRT_REG_HRS", (333, 69), (334, 281));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CCRT_OT_HRS", (335, 70), (336, 282));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JRY_SHRF_HRS", (337, 75), (338, 341));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_ADMN_HRS", (339, 72), (340, 428));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CVF_WARR_HRS",
            (341, 92),
            (342, 93),
            (343, 296),
            (344, 297)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_WARR_REG_HRS", (345, 92), (346, 296));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_WARR_OT_HRS", (347, 93), (348, 297));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CVF_CRTORD_HRS",
            (349, 80),
            (350, 81),
            (351, 284),
            (352, 285)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_CRTORD_REG_HRS", (353, 80), (354, 284));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_CRTORD_OT_HRS", (355, 81), (356, 285));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CVF_DOCS_HRS",
            (357, 84),
            (358, 85),
            (359, 288),
            (360, 289)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_DOCS_REG_HRS", (361, 84), (362, 288));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_DOCS_OT_HRS", (363, 85), (364, 289));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CVF_OTHR_HRS",
            (365, 88),
            (366, 89),
            (367, 292),
            (368, 293)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_OTHR_REG_HRS", (369, 88), (370, 292));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CVF_OTHR_OT_HRS", (371, 89), (372, 293));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRIM_WARR_HRS",
            (373, 108),
            (374, 109),
            (375, 312),
            (376, 313)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_WARR_REG_HRS", (377, 108), (378, 312));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_WARR_OT_HRS", (379, 109), (380, 313));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRIM_CRTORD_HRS",
            (381, 96),
            (382, 97),
            (383, 300),
            (384, 301)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_CRTORD_REG_HRS", (385, 96), (386, 300));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_CRTORD_OT_HRS", (387, 97), (388, 301));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRIM_DOCS_HRS",
            (389, 100),
            (390, 101),
            (391, 304),
            (392, 305)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_DOCS_REG_HRS", (393, 100), (394, 304));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_DOCS_OT_HRS", (395, 101), (396, 305));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CRIM_OTHR_HRS",
            (397, 104),
            (398, 105),
            (399, 308),
            (400, 309)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_OTHR_REG_HRS", (401, 104), (402, 308));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CRIM_OTHR_OT_HRS", (403, 105), (404, 309));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PTO_HRS",
            (405, 429),
            (406, 430),
            (407, 431),
            (408, 432)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PTO_REG_HRS", (409, 429), (410, 431));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PTO_OT_HRS", (411, 430), (412, 432));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "INO_HRS",
            (413, 206),
            (414, 207),
            (415, 359),
            (416, 360)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "INO_REG_HRS", (417, 206), (418, 359));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "INO_OT_HRS", (419, 207), (420, 360));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "BRDI_HRS",
            (421, 210),
            (422, 211),
            (423, 363),
            (424, 364)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "BRDI_REG_HRS", (425, 210), (426, 363));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "BRDI_OT_HRS", (427, 211), (428, 364));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "SEDEV_HRS", (429, 212), (430, 365));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "DNA_HRS",
            (431, 199),
            (432, 200),
            (433, 352),
            (434, 353)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "DNA_REG_HRS", (435, 199), (436, 352));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "DNA_OT_HRS", (437, 200), (438, 353));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "DNA_SAMPLES_CNT", (439, 201), (440, 354));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "ADMIN_HRS",
            (441, 189),
            (442, 190),
            (443, 342),
            (444, 343)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "ADMIN_REG_HRS", (445, 189), (446, 342));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "ADMIN_OT_HRS", (447, 190), (448, 343));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CPIC_JUSTIN_HRS",
            (449, 193),
            (450, 194),
            (451, 346),
            (452, 347)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JUSTIN_REG_HRS", (453, 193), (454, 346));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JUSTIN_OT_HRS", (455, 194), (456, 347));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "CPIC_JA_HRS",
            (457, 191),
            (458, 192),
            (459, 344),
            (460, 345)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JA_REG_HRS", (461, 191), (462, 344));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "CPIC_JA_OT_HRS", (463, 192), (464, 345));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "SIR_HRS",
            (465, 197),
            (466, 198),
            (467, 350),
            (468, 351)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SIR_REG_HRS", (469, 197), (470, 350));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "SIR_OT_HRS", (471, 198), (472, 351));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "VEH_MGNT_HRS",
            (473, 202),
            (474, 203),
            (475, 355),
            (476, 356)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VEH_MGNT_REG_HRS", (477, 202), (478, 355));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "VEH_MGNT_OT_HRS", (479, 203), (480, 356));
        AddMappings(
            mappings,
            BiOracleTargetTable.SheriffServices,
            "PIO_IO_HRS",
            (481, 204),
            (482, 205),
            (483, 357),
            (484, 358)
        );
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PIO_IO_REG_HRS", (485, 204), (486, 357));
        AddMappings(mappings, BiOracleTargetTable.SheriffServicesHours, "PIO_IO_OT_HRS", (487, 205), (488, 358));

        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CCRT_KMS", (489, 366));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L1", (490, 367));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L2", (491, 368));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "AIR_ESC_TRIPS_L3", (492, 369));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L1", (493, 371));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L2", (494, 372));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_TRIPS_L3", (495, 373));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L1", (496, 375));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L2", (497, 376));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_ESC_KMS_L3", (498, 377));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "GRD_KM_TRAVL", (499, 375), (500, 376), (501, 377));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_JRS_SMND", (502, 398));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRN_PNLS", (503, 399));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_SUMD", (504, 400));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PAID", (505, 401));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PANELS_CNT", (506, 402));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "JURORS_PAID_SUM", (507, 403));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "HLD_CELL_HRS", (508, 407));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_CRTORD_RCVD", (509, 433));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_CRTORD_CONC", (510, 434));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_WARR_RCVD", (511, 439));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CVF_WARR_CONC", (512, 440));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_CRTORD_RCVD", (513, 441));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_CRTORD_CONC", (514, 442));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_WARR_RCVD", (515, 447));
        AddMappings(mappings, BiOracleTargetTable.SheriffServices, "CRIM_WARR_CONC", (516, 448));

        var nextId = 517;
        void Add(int scmId, string column, string table = BiOracleTargetTable.SheriffServices) =>
            AddMappings(mappings, table, column, (nextId++, scmId));

        // BCSS_Table_Mapping: twelve independent holding cells, including Federal SEG.
        string[] holdingColumns =
        [
            "HLD_AMAL_CNT",
            "SEG_AMAL_CNT",
            "FED_AMAL_CNT",
            "FED_SEG_AMAL_CNT",
            "HLD_YMAL_CNT",
            "SEG_YMAL_CNT",
            "HLD_AFEM_CNT",
            "SEG_AFEM_CNT",
            "FED_AFEM_CNT",
            "FED_SEG_AFEM_CNT",
            "HLD_YFEM_CNT",
            "SEG_YFEM_CNT",
        ];
        for (var index = 0; index < holdingColumns.Length; index++)
            Add(449 + index, holdingColumns[index]);

        string[] demographicCodes = ["AMAL", "FMAL", "YMAL", "AFEM", "FFEM", "YFEM"];
        string[] classifications = ["L1", "L2", "L3", "SEG_PC"];
        string[] modes = ["AIR", "GRD"];
        for (var mode = 0; mode < modes.Length; mode++)
        {
            for (var demographic = 0; demographic < demographicCodes.Length; demographic++)
            {
                for (var classification = 0; classification < classifications.Length; classification++)
                {
                    var scmId = 461 + mode * 24 + demographic * 4 + classification;
                    var code = demographicCodes[demographic];
                    Add(scmId, $"{modes[mode]}_{code}_CNT_{classifications[classification]}");

                    // Workbook Auto Sum formulas include Provincial adults and youth only.
                    // Federal cells have detailed targets but contribute to neither aggregate.
                    if (demographic is 1 or 4)
                        continue;
                    Add(scmId, $"{modes[mode]}_{code}_CNT");
                    if (classification < 2)
                        Add(scmId, $"{modes[mode]}_LVL{classification + 1}_CNT");
                }
            }
        }

        // Workbook staff-hour Auto Sums: L1 + L2 + L3, regular and overtime.
        foreach (var scmId in new[] { 112, 113, 114, 115, 116, 117, 316, 317, 318, 319, 320, 321 })
            Add(scmId, "AIR_SHRF_HRS");
        foreach (var scmId in new[] { 124, 125, 126, 127, 128, 129, 325, 326, 327, 328, 329, 330 })
            Add(scmId, "GRD_SHRF_HRS");

        Add(LegacyBiOracleStatsSeedData.CircuitTripsId, "CCRT_TRIPS_CNT");
        Add(LegacyBiOracleStatsSeedData.SupervisionRegularId, "SUP_DUTIES_N_FRONT_LINE");
        Add(LegacyBiOracleStatsSeedData.SupervisionOvertimeId, "SUP_DUTIES_N_FRONT_LINE");
        Add(
            LegacyBiOracleStatsSeedData.SupervisionRegularId,
            "SUP_DUTIES_N_FRONT_LINE_REG",
            BiOracleTargetTable.SheriffServicesHours
        );
        Add(
            LegacyBiOracleStatsSeedData.SupervisionOvertimeId,
            "SUP_DUTIES_N_FRONT_LINE_OT",
            BiOracleTargetTable.SheriffServicesHours
        );

        Add(435, "CVF_DOCS_RCVD");
        Add(436, "CVF_DOCS_CONC");
        Add(437, "CVF_OTHR_RCVD");
        Add(438, "CVF_OTHR_CONC");
        Add(443, "CRIM_DOCS_RCVD");
        Add(444, "CRIM_DOCS_CONC");
        Add(445, "CRIM_OTHR_RCVD");
        Add(446, "CRIM_OTHR_CONC");

        return mappings;
    }

    // Mapping IDs are historical keys. Keep existing IDs when editing this list and append new definitions using unused IDs.
    private static void AddMappings(
        ICollection<BiOracleStatMappingSeedDefinition> mappings,
        string targetTable,
        string targetColumn,
        params (int Id, int SubCategoryMetricId)[] entries
    )
    {
        foreach (var (id, subCategoryMetricId) in entries)
        {
            mappings.Add(
                new BiOracleStatMappingSeedDefinition
                {
                    Id = id,
                    SubCategoryMetricId = subCategoryMetricId,
                    TargetTable = targetTable,
                    TargetColumn = targetColumn,
                }
            );
        }
    }
}
