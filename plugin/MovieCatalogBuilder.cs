using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Builds the JSON payload for the current movie library snapshot.
///
/// Schema per movie: title, year, runtime_seconds (RunTimeTicks / 10M),
/// genres, date_added (UTC ISO 8601), tmdb_id. Top-level: generated_at,
/// count, movies.
/// </summary>
public class MovieCatalogBuilder
{
    private readonly ILibraryManager _libraryManager;

    public MovieCatalogBuilder(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public Task<byte[]> BuildAsync(CancellationToken cancellationToken)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie },
            Recursive = true,
            EnableTotalRecordCount = false,
        };

        var movies = _libraryManager.GetItemList(query)
            .OfType<Movie>()
            .Select(Project)
            .ToList();

        var payload = new
        {
            generated_at = DateTime.UtcNow.ToString("O"),
            count = movies.Count,
            movies,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var json = JsonSerializer.Serialize(payload, options);
        return Task.FromResult(Encoding.UTF8.GetBytes(json));
    }

    private static Dictionary<string, object?> Project(Movie m)
    {
        return new Dictionary<string, object?>
        {
            ["title"] = m.Name,
            ["year"] = m.ProductionYear,
            ["runtime_seconds"] = m.RunTimeTicks.HasValue
                ? (long?)(m.RunTimeTicks.Value / 10_000_000)
                : null,
            ["genres"] = m.Genres ?? Array.Empty<string>(),
            ["date_added"] = m.DateCreated.ToUniversalTime().ToString("O"),
            ["tmdb_id"] = m.ProviderIds.TryGetValue("Tmdb", out var tmdb) ? tmdb : null,
        };
    }
}
