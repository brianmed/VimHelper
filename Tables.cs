using System;

using ServiceStack.OrmLite;
using ServiceStack.DataAnnotations;

namespace VimHelper
{
    public class OffsetProperty
    {
        public bool IsStatic { get; set; }

        public string PropertyType { get; set; }

        public string Name { get; set; }
    }

    public class OffsetParamter
    {
        public string Name { get; set; }

        public string TypeName { get; set; }
    }

    public class OffsetMethod
    {
        public bool IsStatic { get; set; }

        public string ReturnType { get; set; }

        public OffsetParamter[] Parameters { get; set; }

        public string Name { get; set; }
    }

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
        
        [Alias("offset_idx")]
        public int Idx { get; set; }

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