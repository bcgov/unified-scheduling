-- ETL_TARGET is the existing local ETL login. MERGE requires the target-table
-- INSERT and UPDATE privileges granted below.
WHENEVER SQLERROR EXIT SQL.SQLCODE
alter session set container = freepdb1;

grant select,insert,update,delete on courts.sheriff_srvces to etl_target;

grant select,insert,update,delete on courts.sheriff_srvces_hours to etl_target;