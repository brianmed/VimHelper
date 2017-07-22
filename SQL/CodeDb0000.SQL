CREATE TABLE code_file (
  code_file_id INTEGER PRIMARY KEY AUTOINCREMENT, 
  code_file_path VARCHAR(128) NOT NULL UNIQUE, 
  code_file_updated VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')),
  code_file_inserted VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')) 
);

CREATE TRIGGER IF NOT EXISTS [UpdatedCodeFile]
    AFTER UPDATE
    ON code_file
    FOR EACH ROW
BEGIN
    UPDATE code_file SET code_file_updated=STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW') WHERE code_file_id=OLD.code_file_id;
END;

---------------
---------------
---------------

CREATE TABLE offset (
  offset_id INTEGER PRIMARY KEY AUTOINCREMENT, 

  offset_code_file_id INTEGER,

  offset_idx INTEGER NOT NULL,

  offset_types_and_things VARCHAR(655360), -- enough for anybody

  offset_updated VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')),
  offset_inserted VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')), 

  FOREIGN KEY(offset_code_file_id) REFERENCES code_file(code_file_id) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TRIGGER IF NOT EXISTS [UpdatedOffset]
    AFTER UPDATE
    ON offset
    FOR EACH ROW
BEGIN
    UPDATE offset SET offset_updated=STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW') WHERE offset_id=OLD.offset_id;
END;
