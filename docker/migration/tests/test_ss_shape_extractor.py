"""Exercise the actual psql report on synthetic data in disposable, per-test schemas.

Requires psql and SS_SHAPE_TEST_CONNECTION_STRING pointing at a disposable PostgreSQL
database where the test user can create schemas. Run with:
python3 -m unittest discover -s docker/migration/tests -v
"""

import os
from pathlib import Path
import subprocess
import unittest
import uuid


EXTRACTOR = (
    Path(__file__).resolve().parents[1]
    / "inputs"
    / "original-extractors"
    / "ss-shape-extractor.sql"
)


class SsShapeExtractorTests(unittest.TestCase):
    def setUp(self):
        self.connection = os.environ.get("SS_SHAPE_TEST_CONNECTION_STRING")
        if not self.connection:
            raise RuntimeError("Set SS_SHAPE_TEST_CONNECTION_STRING to a disposable database.")
        self.schema = "shape_test_" + uuid.uuid4().hex
        self.environment = dict(os.environ, PGOPTIONS=f"-c search_path={self.schema}")
        self.psql(f'CREATE SCHEMA "{self.schema}";')
        self.addCleanup(lambda: self.psql(f'DROP SCHEMA "{self.schema}" CASCADE;'))
        self.psql(
            """
            CREATE TABLE "__EFMigrationsHistory" ("MigrationId" text);
            CREATE TABLE "Location" ("Id" integer, "Name" text);
            CREATE TABLE "User" (
                "Id" uuid, "HomeLocationId" integer DEFAULT 1,
                "Discriminator" text DEFAULT 'Sheriff', "IsEnabled" boolean DEFAULT true,
                "Email" text, "BadgeNumber" text, "Rank" text DEFAULT 'Deputy',
                "IdirName" text, "IdirId" uuid, "KeyCloakId" uuid, "Photo" bytea,
                "Gender" text, "LastLogin" timestamptz, "Excused" boolean DEFAULT false
            );
            CREATE TABLE "UserRole" ("UserId" uuid, "RoleId" integer, "ExpiryDate" timestamptz);
            CREATE TABLE "Role" ("Id" integer, "Name" text);
            CREATE TABLE "LookupCode" ("Id" integer, "Type" text);
            INSERT INTO "LookupCode" VALUES (1, 'Court');
            INSERT INTO "Location" VALUES (1, 'Synthetic location');
            CREATE TABLE "Assignment" (
                "Id" integer, "LookupCodeId" integer DEFAULT 1,
                "Monday" boolean DEFAULT true, "Tuesday" boolean DEFAULT false,
                "Wednesday" boolean DEFAULT false, "Thursday" boolean DEFAULT false,
                "Friday" boolean DEFAULT false, "Saturday" boolean DEFAULT false,
                "Sunday" boolean DEFAULT false, "Start" interval DEFAULT '08:00',
                "End" interval DEFAULT '16:00', "AdhocStartDate" timestamptz,
                "ExpiryDate" timestamptz, "Name" text DEFAULT 'Synthetic',
                "Comment" text, "ExpiryReason" text
            );
            """
        )
        extras = {
            "Shift": "",
            "Duty": ', "AssignmentId" integer',
            "DutySlot": "",
            "SheriffLeave": ', "ExpiryReason" text',
            "SheriffTraining": (
                ', "Note" text, "ExpiryReason" text,'
                ' "TrainingCertificationExpiry" date, "FirstNotice" boolean DEFAULT false'
            ),
            "SheriffAwayLocation": "",
            "SheriffActingRank": "",
        }
        for table, extra in extras.items():
            self.psql(
                f"""
                CREATE TABLE "{table}" (
                    "LocationId" integer DEFAULT 1, "SheriffId" uuid,
                    "StartDate" timestamptz DEFAULT '2026-01-05 16:00Z',
                    "EndDate" timestamptz DEFAULT '2026-01-06 00:00Z',
                    "ExpiryDate" timestamptz, "Timezone" text DEFAULT 'America/Vancouver',
                    "Comment" text {extra}
                );
                """
            )

    def psql(self, sql=None, *, report=False):
        command = [
            "psql", self.connection, "-X", "-q", "-w", "-A", "-t",
            "-F", "|", "-v", "ON_ERROR_STOP=1",
        ]
        if report:
            command.extend(["-f", str(EXTRACTOR)])
        result = subprocess.run(
            command,
            input=sql,
            text=True,
            capture_output=True,
            env=self.environment,
            check=False,
            timeout=60,
        )
        self.assertEqual(0, result.returncode, result.stderr)
        return result.stdout.strip()

    def report(self):
        # Only the extraction timestamp is nondeterministic; compare every result section.
        return "\n".join(
            line for line in self.psql(report=True).splitlines()
            if not line.startswith("Extracted at (UTC) |")
        )

    @staticmethod
    def field(report, name):
        for line in report.splitlines():
            cells = line.split(" | ")
            if cells[0] == name:
                return cells[1:]
        raise AssertionError(f"Missing field: {name}")

    def users(self, count, enabled=None):
        if enabled is None:
            enabled = count
        self.psql(
            f"""
            TRUNCATE "User";
            INSERT INTO "User" ("Id", "IsEnabled")
            SELECT md5(g::text)::uuid, g <= {enabled} FROM generate_series(1, {count}) g;
            """
        )

    def test_review_example_remains_hidden_with_totals_in_other_fields_and_sections(self):
        self.users(10)
        reports = []
        for small in (3, 4):
            self.psql(
                f"""
                TRUNCATE "Assignment";
                INSERT INTO "Assignment" ("Id", "Comment")
                SELECT g, CASE WHEN g <= {small} THEN 'abc' END
                FROM generate_series(1, 100) g;
                """
            )
            self.assertEqual(
                f"100|{100 - small}|{small}",
                self.psql(
                    """SELECT count(*), count(*) FILTER (WHERE "Comment" IS NULL),
                       count(*) FILTER (WHERE "Comment" IS NOT NULL) FROM "Assignment";"""
                ),
            )
            report = self.report()
            for name in ("Assignment.Comment", "Assignment.Name", "Assignment.ExpiryReason"):
                self.assertEqual(["suppressed"] * 7, self.field(report, name))
            # Unaffected tables still retain useful statistics.
            self.assertEqual(["10", "10", "0", "0", "0", "0", "0"], self.field(report, "User.Email"))
            # SH-06 independently publishes the assignment total (100).
            self.assertIn("Court | M------ | 08:00 | 16:00 | no | 100 | 0", report)
            reports.append(report)
        self.assertEqual(*reports)

    def test_multiple_small_cells_cannot_be_solved_from_a_known_total(self):
        reports = []
        for short in (3, 4):
            self.psql(
                f"""
                TRUNCATE "Assignment";
                INSERT INTO "Assignment" ("Id", "Comment")
                SELECT g, CASE WHEN g <= {short} THEN 'abc'
                               WHEN g <= {short + 4} THEN repeat('x', 30) ELSE '' END
                FROM generate_series(1, 8) g;
                """
            )
            reports.append(self.report())
        # In particular, two "<5" cells summing to 8 must not disclose 4 + 4.
        self.assertEqual(*reports)
        self.assertEqual(["suppressed"] * 7, self.field(reports[0], "Assignment.Comment"))

    def test_user_totals_are_not_republished_by_other_fields_or_sh08(self):
        self.psql(
            """
            INSERT INTO "Shift" ("SheriffId")
            SELECT md5('1')::uuid FROM generate_series(1, 6);
            """
        )
        reports = []
        for disabled in (3, 4):
            self.users(5 + disabled, enabled=5)
            report = self.report()
            for name in ("User.Email", "User.Rank", "User.IsEnabled (true)"):
                self.assertEqual(["suppressed"] * 7, self.field(report, name))
            sh08 = report.split("## SH-08 Users, Roles And Home Locations", 1)[1]
            counts = [
                line.split(" | ")[-1] for line in sh08.splitlines()
                if " | " in line and not line.startswith(("Measure |", "--- |"))
            ]
            self.assertTrue(counts)
            self.assertEqual({"suppressed"}, set(counts))
            # SH-01 still discloses five enabled sheriffs; it must not reveal the
            # small disabled count via a total elsewhere.
            self.assertIn("Synthetic location | 2026 | 6 | 0 | 0 | 0 | 0 | 0 | 0 | 5", report)
            reports.append(report)
        self.assertEqual(*reports)

    def test_zero_and_five_counts_are_not_unnecessarily_suppressed(self):
        self.users(10)
        self.psql(
            """
            INSERT INTO "Assignment" ("Id", "Comment")
            SELECT g, CASE WHEN g <= 5 THEN 'abc' END FROM generate_series(1, 10) g;
            """
        )
        report = self.report()
        self.assertEqual(["10", "5", "0", "5", "0", "0", "0"], self.field(report, "Assignment.Comment"))
        self.assertEqual(["10", "0", "0", "10", "0", "0", "0"], self.field(report, "Assignment.Name"))
        self.assertIn("Discriminator / enabled | Sheriff / enabled | 10", report)

    def test_empty_and_under_five_tables_do_not_expose_counts(self):
        empty = self.report()
        self.assertNotIn(":field_presence_rows", empty)
        self.assertFalse(any(line.startswith("Assignment.") for line in empty.splitlines()))
        reports = []
        for count in (1, 4):
            self.psql(
                f"""
                TRUNCATE "Assignment";
                INSERT INTO "Assignment" ("Id") SELECT g FROM generate_series(1, {count}) g;
                """
            )
            reports.append(self.report())
        self.assertEqual(*reports)
        self.assertEqual(["suppressed"] * 7, self.field(reports[0], "Assignment.Name"))


if __name__ == "__main__":
    unittest.main()
