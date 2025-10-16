namespace Choosr.Web.Services;

public record CategoryDto(string Key, string Name, string Icon);

public interface ICategoryService
{
    IEnumerable<CategoryDto> GetAll();
    CategoryDto? Get(string key);
}

public class InMemoryCategoryService : ICategoryService
{
    private readonly List<CategoryDto> _categories = new()
    {
        new("alisveris","Alışveriş","🛍"),
        new("anime","Anime","⚙"),
        new("ask","Aşk","❤️"),
        new("bilim","Bilim","🧪"),
        new("doga","Doğa","🌿"),
        new("eglence","Eğlence","🎭"),
        new("film","Film","🎬"),
        new("guzellik","Güzellik","💄"),
        new("hayvanlar","Hayvanlar","🐾"),
        new("is","İş","💼"),
        new("moda","Moda","👗"),
        new("muzik","Müzik","🎵"),
        new("oyun","Oyun","🎮"),
        new("politika","Politika","🏛"),
        new("spor","Spor","🏀"),
        new("tarih","Tarih","📜"),
        new("teknoloji","Teknoloji","💻"),
        new("yayinci","Yayıncı","📺"),
        new("yasam","Yaşam","🌎"),
        new("yemek","Yemek","🍽")
    };

    public IEnumerable<CategoryDto> GetAll() => _categories;
    public CategoryDto? Get(string key) => _categories.FirstOrDefault(c=>c.Key==key);
}