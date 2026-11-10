namespace BookManager.Models;

public class BookListViewModel
{
    public List<Book> Books { get; set; } = new();
    public string? Term { get; set; }
    public string Sort { get; set; } = "name";
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 5;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; } = 1;
}
