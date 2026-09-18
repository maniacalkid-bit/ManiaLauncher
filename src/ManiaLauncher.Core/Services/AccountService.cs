using ManiaLauncher.Core.Models;
using Newtonsoft.Json;

namespace ManiaLauncher.Core.Services;

public class AccountService
{
    private readonly string _accountsFile;
    private List<Account> _accounts = new();

    public AccountService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _accountsFile = Path.Combine(dataDirectory, "accounts.json");
        Load();
    }

    public IReadOnlyList<Account> Accounts => _accounts.AsReadOnly();

    public void Load()
    {
        if (!File.Exists(_accountsFile))
        {
            _accounts = new List<Account>();
            return;
        }

        var json = File.ReadAllText(_accountsFile);
        _accounts = JsonConvert.DeserializeObject<List<Account>>(json) ?? new List<Account>();
    }

    public void Save()
    {
        var json = JsonConvert.SerializeObject(_accounts, Formatting.Indented);
        File.WriteAllText(_accountsFile, json);
    }

    public void Add(Account account)
    {
        // Avoid duplicates by username + type
        _accounts.RemoveAll(a => a.Username.Equals(account.Username, StringComparison.OrdinalIgnoreCase)
                                 && a.Type == account.Type);
        _accounts.Add(account);
        Save();
    }

    public void Remove(string id)
    {
        _accounts.RemoveAll(a => a.Id == id);
        Save();
    }

    public Account? GetById(string id) => _accounts.FirstOrDefault(a => a.Id == id);
}
