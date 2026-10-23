using System.Text.Json;

class RequestHistory
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }
    public int? OldEmployeeId { get; set; }
    public string? OldEmployeeName { get; set; }
    public int? NewEmployeeId { get; set; }
    public string? NewEmployeeName { get; set; }
    public string? Comment { get; set; }
}

class HistoryStore
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly List<RequestHistory> _items;
    private int _nextId;

    public HistoryStore(string path)
    {
        _path = path;
        _items = File.Exists(path)
            ? JsonSerializer.Deserialize<List<RequestHistory>>(File.ReadAllText(path)) ?? new List<RequestHistory>()
            : new List<RequestHistory>();
        _nextId = _items.Count == 0 ? 1 : _items.Max(x => x.Id) + 1;
    }

    public void Add(RequestHistory item)
    {
        lock (_lock)
        {
            item.Id = _nextId++;
            item.ChangedAt = DateTime.Now;
            _items.Add(item);
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось записать историю в файл: " + ex.Message);
            }
        }
    }

    public List<RequestHistory> GetByRequest(int requestId)
    {
        lock (_lock)
        {
            return _items.Where(x => x.RequestId == requestId)
                         .OrderBy(x => x.ChangedAt).ThenBy(x => x.Id).ToList();
        }
    }
}