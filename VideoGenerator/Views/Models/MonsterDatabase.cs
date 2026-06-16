using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VideoGenerator.Views.Models
{
    public class MonsterDatabase
    {
        public List<string> Epic { get; set; } = new();
        public List<string> Large { get; set; } = new();

        [JsonIgnore]
        public List<string> All => Epic.Concat(Large).Distinct().OrderBy(x => x).ToList();
    }
}
