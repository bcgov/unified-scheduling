-- Passwordless owner for the mock BIPRDZ destination.
-- ETL connects as ETL_TARGET and receives table-level DML grants separately.
WHENEVER SQLERROR EXIT SQL.SQLCODE
alter session set container = freepdb1;

create user courts no authentication
   default tablespace users
   quota unlimited on users;