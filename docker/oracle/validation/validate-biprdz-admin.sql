-- Run as SYS in the Oracle container after initializing a fresh volume.
WHENEVER SQLERROR EXIT SQL.SQLCODE
alter session set container = freepdb1;
   SET SERVEROUTPUT ON

declare
   v_count number;
begin
   select count(*)
     into v_count
     from all_tables
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' );
   if v_count <> 2 then
      raise_application_error(
         -20021,
         'COURTS destination tables are missing'
      );
   end if;
   select count(*)
     into v_count
     from all_tab_columns
    where owner = 'COURTS'
      and table_name = 'SHERIFF_SRVCES';
   if v_count <> 201 then
      raise_application_error(
         -20022,
         'SHERIFF_SRVCES must have 201 columns'
      );
   end if;
   select count(*)
     into v_count
     from all_tab_columns
    where owner = 'COURTS'
      and table_name = 'SHERIFF_SRVCES_HOURS';
   if v_count <> 129 then
      raise_application_error(
         -20023,
         'SHERIFF_SRVCES_HOURS must have 129 columns'
      );
   end if;
   select count(*)
     into v_count
     from dba_sys_privs
    where grantee = 'COURTS';
   if v_count <> 0 then
      raise_application_error(
         -20024,
         'COURTS must not have system privileges'
      );
   end if;
   select count(*)
     into v_count
     from dba_role_privs
    where grantee = 'COURTS';
   if v_count <> 0 then
      raise_application_error(
         -20025,
         'COURTS must not have roles'
      );
   end if;
   select count(*)
     into v_count
     from dba_users
    where username = 'COURTS'
      and authentication_type = 'NONE';
   if v_count <> 1 then
      raise_application_error(
         -20026,
         'COURTS must use NO AUTHENTICATION'
      );
   end if;
   select count(*)
     into v_count
     from all_constraints
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' );
   if v_count <> 0 then
      raise_application_error(
         -20027,
         'COURTS destination has undocumented constraints'
      );
   end if;
   select count(*)
     into v_count
     from all_indexes
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' );
   if v_count <> 0 then
      raise_application_error(
         -20028,
         'COURTS destination has undocumented indexes'
      );
   end if;
   select count(*)
     into v_count
     from all_triggers
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' );
   if v_count <> 0 then
      raise_application_error(
         -20029,
         'COURTS destination has undocumented triggers'
      );
   end if;
   select count(*)
     into v_count
     from all_tab_columns
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' )
      and ( nullable = 'N'
       or data_default_vc is not null
       or virtual_column = 'YES'
       or identity_column = 'YES' );
   if v_count <> 0 then
      raise_application_error(
         -20030,
         'COURTS destination has undocumented column behaviour'
      );
   end if;
   select count(*)
     into v_count
     from dba_tab_privs
    where owner = 'COURTS'
      and grantee = 'ETL_TARGET'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' )
      and privilege in ( 'SELECT',
                         'INSERT',
                         'UPDATE',
                         'DELETE' );
   if v_count <> 8 then
      raise_application_error(
         -20031,
         'ETL_TARGET requires four DML grants on each destination table'
      );
   end if;
   select count(*)
     into v_count
     from dba_tab_privs
    where owner = 'COURTS'
      and table_name in ( 'SHERIFF_SRVCES',
                          'SHERIFF_SRVCES_HOURS' )
      and ( grantee <> 'ETL_TARGET'
       or privilege not in ( 'SELECT',
                             'INSERT',
                             'UPDATE',
                             'DELETE' ) );
   if v_count <> 0 then
      raise_application_error(
         -20032,
         'COURTS destination has unexpected object grants'
      );
   end if;
end;
/

select owner,
       table_name
  from all_tables
 where owner in ( 'ETL_TARGET',
                  'COURTS' )
 order by owner,
          table_name;

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

pro    BIPRDZ mock admin validation succeeded.