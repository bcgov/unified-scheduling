-- Run while connected as ETL_TARGET in FREEPDB1. The smoke rows are rolled back.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET SERVEROUTPUT ON

declare
   v_services_columns number;
   v_hours_columns    number;
begin
   select count(*)
     into v_services_columns
     from all_tab_columns
    where owner = 'COURTS'
      and table_name = 'SHERIFF_SRVCES';

   select count(*)
     into v_hours_columns
     from all_tab_columns
    where owner = 'COURTS'
      and table_name = 'SHERIFF_SRVCES_HOURS';

   if v_services_columns <> 201 then
      raise_application_error(
         -20011,
         'SHERIFF_SRVCES must have 201 columns'
      );
   end if;
   if v_hours_columns <> 129 then
      raise_application_error(
         -20012,
         'SHERIFF_SRVCES_HOURS must have 129 columns'
      );
   end if;
end;
/

select table_name,
       count(*) as column_count
  from all_tab_columns
 where owner = 'COURTS'
   and table_name in ( 'SHERIFF_SRVCES',
                       'SHERIFF_SRVCES_HOURS' )
 group by table_name
 order by table_name;

select table_name,
       column_id,
       column_name,
       data_type,
       data_precision,
       data_scale,
       data_length,
       char_used,
       nullable,
       data_default,
       virtual_column,
       identity_column
  from all_tab_columns
 where owner = 'COURTS'
   and table_name in ( 'SHERIFF_SRVCES',
                       'SHERIFF_SRVCES_HOURS' )
 order by table_name,
          column_id;

savepoint before_biprdz_smoke_test;

delete from courts.sheriff_srvces
 where crt_loc_id = 999999
   and month = date '1900-01-01'
   and supervisor_yn = 'T';

insert into courts.sheriff_srvces (
   crt_loc_id,
   month,
   supervisor_yn,
   scv_jy_shrf_hrs
) values
   ( 999999,
     date '1900-01-01',
     'T',
     1.25 );

update courts.sheriff_srvces
   set
   scv_jy_shrf_hrs = 2.50
 where crt_loc_id = 999999
   and month = date '1900-01-01'
   and supervisor_yn = 'T';

merge into courts.sheriff_srvces destination
using (
   select 999999 as crt_loc_id,
          date '1900-01-01' as month,
          'T' as supervisor_yn,
          3.75 as scv_jy_shrf_hrs
     from dual
) source on ( destination.crt_loc_id = source.crt_loc_id
   and destination.month = source.month
   and destination.supervisor_yn = source.supervisor_yn )
when matched then update
set scv_jy_shrf_hrs = source.scv_jy_shrf_hrs
when not matched then
insert (
   crt_loc_id,
   month,
   supervisor_yn,
   scv_jy_shrf_hrs )
values
   ( source.crt_loc_id,
     source.month,
     source.supervisor_yn,
     source.scv_jy_shrf_hrs );

delete from courts.sheriff_srvces_hours
 where crt_loc_id = 999999
   and month = date '1900-01-01'
   and supervisor_yn = 'T';

insert into courts.sheriff_srvces_hours (
   crt_loc_id,
   month,
   supervisor_yn,
   ca_reg_reg_hrs
) values
   ( 999999,
     date '1900-01-01',
     'T',
     4.25 );

delete from courts.sheriff_srvces
 where crt_loc_id = 999999
   and month = date '1900-01-01'
   and supervisor_yn = 'T';

delete from courts.sheriff_srvces_hours
 where crt_loc_id = 999999
   and month = date '1900-01-01'
   and supervisor_yn = 'T';

rollback to before_biprdz_smoke_test;

pro    BIPRDZ mock metadata and DML smoke validation succeeded.