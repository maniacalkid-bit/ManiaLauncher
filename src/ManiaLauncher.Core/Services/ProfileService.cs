using ManiaLauncher.Core.Models;
using Newtonsoft.Json;

namespace ManiaLauncher.Core.Services;

public class ProfileService
{
    private readonly string _profilesFile;
    private List<Profile> _profiles = new();

    public ProfileService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _profilesFile = Path.Combine(dataDirectory, "profiles.json");
        Load();
    }

    public IReadOnlyList<Profile> Profiles => _profiles.AsReadOnly();

    public void Load()
    {
        if (!File.Exists(_profilesFile))
        {
            _profiles = new List<Profile>();
            return;
        }

        var json = File.ReadAllText(_profilesFile);
        _profiles = JsonConvert.DeserializeObject<List<Profile>>(json) ?? new List<Profile>();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(_profiles, Formatting.Indented);
        File.WriteAllText(_profilesFile, json);
    }

    public Profile Create(string name, string versionId, string accountId)
    {
        var profile = new Profile
        {
            Name = name,
            VersionId = versionId,
            AccountId = accountId
        };
        _profiles.Add(profile);
        Save();
        return profile;
    }

    public void Update(Profile profile)
    {
        var index = _profiles.FindIndex(p => p.Id == profile.Id);
        if (index >= 0)
        {
            _profiles[index] = profile;
            Save();
        }
    }

    public void Delete(string id)
    {
        _profiles.RemoveAll(p => p.Id == id);
        Save();
    }

    public Profile? GetById(string id) => _profiles.FirstOrDefault(p => p.Id == id);
}
