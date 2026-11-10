using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using BookManager.Models;

namespace BookManager.Services;

public class BookService
{
    private readonly object _gate = new();
    private readonly string _dataFile;
    private readonly string _uploadFolder;
    private readonly ILogger<BookService> _logger;
    private List<Book> _books = new();

    public BookService(IWebHostEnvironment environment, ILogger<BookService> logger)
    {
        _logger = logger;
        string folder = Path.Combine(environment.ContentRootPath, "Data");
        _dataFile = Path.Combine(folder, "books.json");
        _uploadFolder = Path.Combine(folder, "Uploads");
        Directory.CreateDirectory(_uploadFolder);

        if (File.Exists(_dataFile))
        {
            _books = JsonSerializer.Deserialize<List<Book>>(File.ReadAllText(_dataFile))
                ?? throw new InvalidDataException("Data/books.json không chứa danh sách sách hợp lệ.");
        }
        else
        {
            // Dữ liệu minh họa để thử tìm kiếm, sắp xếp và phân trang.
            var samples = Enumerable.Range(1, 12).Select(i => new Book
            {
                Id = i,
                Name = $"Sách thực hành {i:00}",
                Authors = $"Tác giả mẫu {(i - 1) % 3 + 1}",
                Publisher = $"Nhà xuất bản mẫu {(i - 1) % 2 + 1}",
                Year = 2020 + (i - 1) % 6,
                Description = "Dữ liệu minh họa của bài thực hành."
            }).ToList();
            Commit(samples);
        }
    }

    private static Book Copy(Book b) => new()
    {
        Id = b.Id, Name = b.Name, Authors = b.Authors,
        Publisher = b.Publisher, Year = b.Year,
        Description = b.Description, DataFile = b.DataFile
    };

    // Ghi xong JSON mới cập nhật danh sách trong bộ nhớ.
    private void Commit(List<Book> books)
    {
        string temporary = _dataFile + ".tmp";
        string json = JsonSerializer.Serialize(books,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temporary, json);
        File.Move(temporary, _dataFile, overwrite: true);
        _books = books;
    }

    public Book? Get(int id)
    {
        lock (_gate)
        {
            var book = _books.FirstOrDefault(b => b.Id == id);
            return book is null ? null : Copy(book);
        }
    }

    public void Add(Book book)
    {
        lock (_gate)
        {
            book.Id = _books.Count == 0 ? 1 : _books.Max(b => b.Id) + 1;
            var books = _books.Select(Copy).ToList();
            books.Add(Copy(book));
            Commit(books);
        }
    }

    public bool Update(Book book)
    {
        lock (_gate)
        {
            var books = _books.Select(Copy).ToList();
            int index = books.FindIndex(b => b.Id == book.Id);
            if (index < 0) return false;
            books[index] = Copy(book);
            Commit(books);
            return true;
        }
    }

    public bool Delete(int id)
    {
        lock (_gate)
        {
            if (!_books.Any(b => b.Id == id)) return false;
            Commit(_books.Where(b => b.Id != id).Select(Copy).ToList());
            return true;
        }
    }

    public BookListViewModel GetPage(string? term, string? sort, int pageNumber)
    {
        List<Book> snapshot;
        lock (_gate) { snapshot = _books.Select(Copy).ToList(); }

        IEnumerable<Book> query = snapshot;
        term = term?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(b =>
                (b.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (b.Authors ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (b.Publisher ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (b.Description ?? "").Contains(term, StringComparison.OrdinalIgnoreCase) ||
                b.Year.ToString().Contains(term));
        }

        string[] options = { "name", "name_desc", "authors", "authors_desc",
            "publisher", "publisher_desc", "year", "year_desc" };
        if (sort is null || !options.Contains(sort)) sort = "name";

        IOrderedEnumerable<Book> ordered = sort switch
        {
            "name_desc" => query.OrderByDescending(b => b.Name),
            "authors" => query.OrderBy(b => b.Authors),
            "authors_desc" => query.OrderByDescending(b => b.Authors),
            "publisher" => query.OrderBy(b => b.Publisher),
            "publisher_desc" => query.OrderByDescending(b => b.Publisher),
            "year" => query.OrderBy(b => b.Year),
            "year_desc" => query.OrderByDescending(b => b.Year),
            _ => query.OrderBy(b => b.Name)
        };

        const int size = 5;
        int total = query.Count();
        int pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        pageNumber = Math.Clamp(pageNumber, 1, pages);

        return new BookListViewModel
        {
            Books = ordered.ThenBy(b => b.Id)
                .Skip((pageNumber - 1) * size).Take(size).ToList(),
            Term = term, Sort = sort, PageNumber = pageNumber,
            PageSize = size, TotalItems = total, TotalPages = pages
        };
    }

    public string GetUploadPath(string name) =>
        Path.Combine(_uploadFolder, Path.GetFileName(name));

    public async Task<string?> UploadAsync(IFormFile? file)
    {
        if (file is null) return null;
        if (file.Length < 5)
            throw new ValidationException("File PDF đang rỗng hoặc không hợp lệ.");
        if (file.Length > 10 * 1024 * 1024)
            throw new ValidationException("File phải có dung lượng tối đa 10 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf",
            StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Chỉ chấp nhận file PDF.");

        // Kiểm tra dấu hiệu PDF cơ bản; đây không phải bộ quét mã độc.
        await using (var input = file.OpenReadStream())
        {
            byte[] header = new byte[5];
            await input.ReadExactlyAsync(header);
            if (Encoding.ASCII.GetString(header) != "%PDF-")
                throw new ValidationException("Nội dung file không có phần đầu PDF hợp lệ.");
        }

        string name = $"{Guid.NewGuid():N}.pdf";
        try
        {
            await using var output = new FileStream(GetUploadPath(name),
                FileMode.CreateNew, FileAccess.Write);
            await file.CopyToAsync(output);
        }
        catch
        {
            TryDeleteUpload(name);
            throw;
        }
        return name;
    }

    // Chỉ dùng để dọn file mới nếu lần lưu thông tin sách thất bại.
    public void TryDeleteUpload(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try { File.Delete(GetUploadPath(name)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.LogWarning(ex, "Không dọn được file tải lên chưa sử dụng."); }
    }
}
