using LP.Common;
using Microsoft.EntityFrameworkCore;

namespace LP.Entity.Store;

public sealed class InterestsStore
{
    private readonly ApplicationContext _db;
    public InterestsStore(ApplicationContext db) => _db = db;

    public List<Interest> List()
    {
        var list = _db.Interests
            .IgnoreQueryFilters()
            .OrderBy(x=>x.Group)
            .ToList();
        if (list.Count == 0)
        {
            var interest = new Interest() {Id = Guid.NewGuid(), Name = "прогулки", Path = ""};
            _db.Interests.Add(interest);
            _db.SaveChangesAsync();
        }

        return list;
    }
}