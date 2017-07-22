using System;

using ServiceStack.OrmLite;
using ServiceStack.DataAnnotations;

namespace VimHelper
{
    [Alias("code_file")]
    public class CodeFileTable
    {
        [AutoIncrement]
        [Alias("code_file_id")]
        public long Id { get; set; }
        
        [Index(Unique = true)]
        [Alias("code_file_path")]
        public string Path { get; set; }
        
        [Alias("code_file_updated")]
        public DateTime Updated { get; set; }

        [Alias("code_file_inserted")]
        public DateTime Inserted { get; set; }
    }
    
    [Alias("offset")]
    public class OffsetTable
    {
        [AutoIncrement]
        [Alias("offset_id")]
        public long Id { get; set; }
        
      	[ForeignKey(typeof(CodeFileTable), OnDelete = "CASCADE", OnUpdate = "CASCADE")]
        [Alias("offset_code_file_id")]
        public long CodeFileId { get; set; }

        [Alias("offset_types_and_things")]
        public string TypesAndThings { get; set; }

        [Alias("offset_updated")]
        public DateTime Updated { get; set; }

        [Alias("offset_inserted")]
        public DateTime Inserted { get; set; }
    }
}