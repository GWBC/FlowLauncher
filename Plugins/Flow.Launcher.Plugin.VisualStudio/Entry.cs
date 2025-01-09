using System;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.VisualStudio
{
    //For Json deserialization.
    public class Entry 
    {
        public string Key { get; set; }
        public Value Value { get; set; }

        //NonJson
        [JsonIgnore]
        public string Path => Value.LocalProperties.FullPath;
        [JsonIgnore]
        public int ItemType => Value.LocalProperties.Type;

        public override bool Equals(object obj)
        {
            return obj is Entry entry &&
                   Key == entry.Key;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key);
        }
    }

    public class Value
    {
        public LocalProperties LocalProperties { get; set; }
        public object Remote { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime LastAccessed { get; set; }

        public bool IsLocal { get; set; }
        public bool HasRemote { get; set; }
        public bool IsSourceControlled { get; set; }
    }

    public class LocalProperties
    {
        public string FullPath { get; set; }
        //0 is a solution/project.
        //1 is a file
        public int Type { get; set; }
        public object SourceControl { get; set; }
    }
}
