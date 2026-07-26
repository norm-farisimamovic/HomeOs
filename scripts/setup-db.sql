-- Home OS — create the local development database and user.
-- Matches the dev connection string in backend/src/HomeOs.Api/appsettings.Development.json.
CREATE DATABASE IF NOT EXISTS homeos CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS 'homeos'@'localhost' IDENTIFIED BY 'homeos';
GRANT ALL PRIVILEGES ON homeos.* TO 'homeos'@'localhost';
FLUSH PRIVILEGES;
