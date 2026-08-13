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

            return events.Where(ev => MatchesEventCore(ev, charFilter, status, query)).ToList();
        }

        public bool MatchesEvent(
            PreviewEventModel pipelineEvent,
            string characterFilter,
            string statusFilter,
            string searchQuery)
        {
            return MatchesEventCore(
                pipelineEvent,
                characterFilter ?? "ALL",
                statusFilter ?? "ALL",
                searchQuery ?? string.Empty);
        }

        private static bool MatchesEventCore(
            PreviewEventModel pipelineEvent,
            string characterFilter,
            string statusFilter,
            string searchQuery)
        {
            if (pipelineEvent == null)
            {
                return false;
            }

            bool matchesCharacter = characterFilter.Equals("ALL", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(pipelineEvent.CharacterName, characterFilter, StringComparison.OrdinalIgnoreCase);
            if (!matchesCharacter)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                bool matchesSearch = (pipelineEvent.FolderName != null &&
                                      pipelineEvent.FolderName.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)) ||
                                     (pipelineEvent.ParsedData?.DisplayText != null &&
                                      pipelineEvent.ParsedData.DisplayText.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
                if (!matchesSearch)
                {
                    return false;
                }
            }

            return statusFilter switch
            {
                "ERRORS" => pipelineEvent.Status == EventStatuses.MissingIcon || pipelineEvent.Status == EventStatuses.NoAudio,
                "PENDING" => pipelineEvent.Status == EventStatuses.Pending || pipelineEvent.Status == EventStatuses.PendingIcon,
                "NEEDS_MAPPING" => pipelineEvent.NeedsMapping,
                _ => true
            };
        }
    }
}
