using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;

namespace SpottyBotApi.Controllers
{
    [ApiController]
    public class PlaylistController : ControllerBase
    {
        private readonly ILogger<PlaylistController> _logger;
        private IMemoryCache _cache;
        private PlaylistNameGenerator _nameGenerator;
        static Random random = new Random();
        public PlaylistController(ILogger<PlaylistController> logger, IMemoryCache memoryCache, PlaylistNameGenerator nameGenerator)
        {
            _logger = logger;
            _cache = memoryCache;
            _nameGenerator = nameGenerator;
        }

        [HttpGet("playlist")]
        public async Task<ActionResult<SearchItem>> GetPlaylist(string keyword = "")
        {
            var accessToken = await GetAccessToken();

            SpotifyWebAPI api = new SpotifyWebAPI
            {
                AccessToken = accessToken.access_token,
                TokenType = "Bearer"
            };

            var randomOffset = random.Next(0, 1000);
            var randomSearch = keyword == "" ? GetRandomSearch() : keyword;

            var response = api.SearchItems(randomSearch, SearchType.Playlist, 50, randomOffset);

            var payload = response.Playlists.Items;

            // return Ok(payload);
            int index = random.Next(payload.Count());
            var singleItem = payload[index];

            return Ok(new { singleItem.Name, singleItem.Uri, TrackCount = singleItem.Tracks.Total, singleItem.Href, singleItem.Id });
        }

        [HttpGet("generate")]
        public async Task<ActionResult<SearchItem>> GeneratePlaylist(string keyword = "")
        {
            //must be a user access token
            var accessToken = ""; //await GetAccessToken();

            SpotifyWebAPI api = new SpotifyWebAPI
            {
                AccessToken = accessToken,
                TokenType = "Bearer"
            };

            var randomOffset = random.Next(0, 1000);
            var randomSearch = keyword == "" ? GetRandomSearch() : keyword;

            TuneableTrack tar = new TuneableTrack
            {
                Popularity = random.NextDouble() >= 0.5 ? 80 : 100
            };
            
            var genre = GetGenreSeed(keyword);
            Recommendations rec = api.GetRecommendations(genreSeed: genre, target: tar, market: random.NextDouble() >= 0.5 ? "GB" : "US");

            var newPlaylist = api.CreatePlaylist("rsacr1m9ge9ur5tdceead2ziy", "Wholeschool Playlist " + _nameGenerator.Generate(), true, false, $"Auto generated playlist for {keyword} on {DateTime.Now.ToShortDateString()}");
            
            ErrorResponse response = api.ReplacePlaylistTracks(newPlaylist.Id, rec.Tracks.Select(s => s.Uri).ToList());
            if(!response.HasError())
                Console.WriteLine("success");
            
            return Ok(new { newPlaylist.Name, newPlaylist.Uri, newPlaylist.Href, newPlaylist.Id });
        }

        private List<string>  GetGenreSeed(string keyword)
        {
          if(string.IsNullOrWhiteSpace(keyword)) return null;

          return new List<string> { keyword };
        }

        [HttpGet("album")]
        public async Task<ActionResult<SearchItem>> GetAlbum(string keyword = "")
        {
            var accessToken = await GetAccessToken();

            SpotifyWebAPI api = new SpotifyWebAPI
            {
                AccessToken = accessToken.access_token,
                TokenType = "Bearer"
            };

            var randomOffset = random.Next(0, 1000);
            var randomSearch = keyword == "" ? GetRandomSearch() : keyword;

            var response = api.SearchItems(randomSearch, SearchType.Album, 50, randomOffset);

            var payload = response.Albums.Items;

            // return Ok(payload);
            int index = random.Next(payload.Count());
            var singleItem = payload[index];

            return Ok(new { singleItem.Name, singleItem.Uri, Artist = string.Join(",", singleItem.Artists), singleItem.Href, singleItem.Id });
        }

        private string GetRandomSearch() 
        {

            string randomCharacter = GetRandomCharacter("abcdefghijklmnopqrstuvwxyz");
            var randomSearch = "";

            switch (random.NextDouble() >= 0.5) {
                case true:
                randomSearch = randomCharacter + '%';
                break;
                case false:
                randomSearch = '%' + randomCharacter + '%';
                break;
            }

            return randomSearch;
         }

        public static string GetRandomCharacter(string text)
        {
            int index = random.Next(text.Length);
            return text[index].ToString();
        }

        //todo - cache for 1 hour and dheck for token in cache
        private async Task<AccessToken> GetAccessToken()
        {
            if (!_cache.TryGetValue("AccessToken", out AccessToken cacheEntry))
            {
                var clientId = Environment.GetEnvironmentVariable("SpotifyClientId");
                var clientSecret = Environment.GetEnvironmentVariable("SpotifyClientSecret");

                Console.WriteLine("Getting Token");
                string credentials = String.Format("{0}:{1}", clientId, clientSecret);

                using (var client = new HttpClient())
                {
                    //Define Headers
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials)));

                    //Prepare Request Body
                    List<KeyValuePair<string, string>> requestData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                };

                    FormUrlEncodedContent requestBody = new FormUrlEncodedContent(requestData);

                    //Request Token
                    var request = await client.PostAsync("https://accounts.spotify.com/api/token", requestBody);
                    var response = await request.Content.ReadAsStringAsync();
                    cacheEntry = JsonConvert.DeserializeObject<AccessToken>(response);
                }

                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()

                    .SetSlidingExpiration(TimeSpan.FromMinutes(60));

                // Save data in cache.
                _cache.Set("AccessToken", cacheEntry, cacheEntryOptions);
            }

            return cacheEntry;

        }

        private static async Task<Paging<SimplePlaylist>> GetAllPlaylistTracks(SpotifyWebAPI api, Paging<SimplePlaylist> playlists)
        {
            if (playlists.Items == null) return null;

            playlists.Items.ForEach(playlist => Console.WriteLine($"- {playlist.Name}"));
            if (playlists.HasNextPage())
                await GetAllPlaylistTracks(api, await api.GetNextPageAsync(playlists));

            return playlists;
        }
    }
}
