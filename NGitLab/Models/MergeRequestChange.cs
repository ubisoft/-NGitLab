using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class MergeRequestChange
    {
        [JsonPropertyName("changes")]
        public Change[] Changes { get; set; }
    }
}
