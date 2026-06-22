SELECT 'CREATE DATABASE tredata' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'tredata')\gexec
