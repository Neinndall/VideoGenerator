using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VideoGenerator.Services
{
    public class MonsterDatabase
    {
        // Schema for the single, merged monsters.json catalog.
        // DefaultMonsters supplies the versioned app seed; new user entries
        // are stored alongside the defaults in this same JSON document.
        public List<string> Epic { get; set; } = new();
        public List<string> Large { get; set; } = new();

        [JsonIgnore]
        public List<string> All => Epic.Concat(Large).Distinct().OrderBy(x => x).ToList();
    }
}
