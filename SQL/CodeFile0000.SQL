CREATE TABLE code_file 
(
  code_file_id INTEGER PRIMARY KEY AUTOINCREMENT, 
  code_file_path VARCHAR(128) NOT NULL UNI, 
  code_file_types_and_things VARCHAR(655360),
  code_file_inserted VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW')), 
  code_file_updated VARCHAR(26) NOT NULL DEFAULT (STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW'))
);
CREATE UNIQUE INDEX uidx_code_file_code_file_path ON code_file (code_file_path);

CREATE TRIGGER IF NOT EXISTS [UpdatedCodeFile]
    AFTER UPDATE
    ON code_file
    FOR EACH ROW
BEGIN
    UPDATE code_file SET code_file_updated=STRFTIME('%Y-%m-%d %H:%M:%f', 'NOW') WHERE code_file_id=OLD.code_file_id;
END;
