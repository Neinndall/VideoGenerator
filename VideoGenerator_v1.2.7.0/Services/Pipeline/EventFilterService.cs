using System;
using System.Collections.Generic;
using System.Linq;
using VideoGenerator.Views.Models;

namespace VideoGenerator.Services
{
    public class EventFilterService
    {
        /// <summary>
        /// Filters a list of events based on character name, status type, and search keyword.
        /// </summary>
        public List<PreviewEventModel> FilterEvents(
            IEnumerable<PreviewEventModel> events, 
            string characterFilter, 
            string statusFilter, 
            string searchQuery)
        {
            if (events == null) return new List<PreviewEventModel>();

            var charFilter = characterFilter ?? "ALL";
            var status = statusFilter ?? "ALL";
            var query = searchQuery ?? "";

            return events.Where(ev => 
            {
                // 1. Character Filter
                bool matchesChar = charFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase) || 
                                   string.Equals(ev.CharacterName, charFilter, StringComparison.OrdinalIgnoreCase);
                if (!matchesChar) return false;

                // 2. Search Query (Matches folder name or parsed display text)
                if (!string.IsNullOrEmpty(query))
                {
                    bool matchesSearch = (ev.FolderName != null && ev.FolderName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                                         (ev.ParsedData != null && ev.ParsedData.DisplayText != null && ev.ParsedData.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase));
                    if (!matchesSearch) return false;
                }

                // 3. Status Filter (ALL / ERRORS / PENDING)
                return status switch
                {
                    "ERRORS" => ev.Status == "Missing Icon" || ev.Status == "No Audio",
                    "PENDING" => ev.Status == "Pending" || ev.Status == "Pending Icon",
                    _ => true
                };
            }).ToList();
        }
    }
}
