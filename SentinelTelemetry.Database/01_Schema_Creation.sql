CREATE TABLE Category 
(
	id_category INT IDENTITY(1,1) PRIMARY KEY,
	category_name NVARCHAR(100) NOT NULL,
	description NVARCHAR(255) NULL
)

CREATE TABLE Source 
(
	id_source INT IDENTITY(1,1) PRIMARY KEY,
	source_name NVARCHAR(100) NOT NULL,
	description NVARCHAR(255) NULL
)

CREATE TABLE Version 
(
	id_version INT IDENTITY(1,1) PRIMARY KEY,
	numero_version INT NOT NULL,
	description NVARCHAR(255) NULL
)

CREATE TABLE Events 
(
	id_event UNIQUEIDENTIFIER PRIMARY KEY NOT NULL,
	timestamp DATETIME2(7) NOT NULL,
	id_category INT NOT NULL,
	id_source INT NOT NULL,
	id_version INT NOT NULL,
	event_type NVARCHAR(100) NOT NULL,
	data_json NVARCHAR(MAX) NOT NULL

	CONSTRAINT fk_events_source   FOREIGN KEY (id_source)   
	REFERENCES dbo.Source (id_source),
    
	CONSTRAINT fk_events_category FOREIGN KEY (id_category) 
	REFERENCES dbo.Category (id_category),
    
	CONSTRAINT fk_events_version  FOREIGN KEY (id_version)  
	REFERENCES dbo.Version (id_version)
)
