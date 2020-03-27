using SpotifyAPI.Web.Models;

namespace SpottyBotApi.Controllers
{
    public class UserPlaylists
    {
        public PrivateProfile Profile { get; set; }
        public Paging<SimplePlaylist> Playlists { get; set; }
    }
}
