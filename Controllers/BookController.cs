using System.ComponentModel.DataAnnotations;
using BookManager.Models;
using BookManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookManager.Controllers;

public class BookController : Controller
{
    private readonly BookService _service;
    private readonly ILogger<BookController> _logger;

    public BookController(BookService service, ILogger<BookController> logger)
    {
        _service = service;
        _logger = logger;
    }

    public IActionResult Index(string? term = null, string? sort = "name", int pageNumber = 1)
        => View(_service.GetPage(term, sort, pageNumber));

    public IActionResult Details(int id)
    {
        var book = _service.Get(id);
        return book is null ? NotFound() : View(book);
    }

    public IActionResult Create() => View(new Book());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(
        [Bind("Name,Authors,Publisher,Year,Description")] Book book,
        IFormFile? file)
    {
        return SaveBookAsync(book, file, isNew: true);
    }

    public IActionResult Edit(int id)
    {
        var book = _service.Get(id);
        return book is null ? NotFound() : View(book);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id,
        [Bind("Id,Name,Authors,Publisher,Year,Description")] Book book,
        IFormFile? file)
    {
        if (id != book.Id)
            return BadRequest();
        var existing = _service.Get(id);
        if (existing is null)
            return NotFound();

        book.DataFile = existing.DataFile;
        return await SaveBookAsync(book, file, isNew: false);
    }

    private async Task<IActionResult> SaveBookAsync(Book book, IFormFile? file, bool isNew)
    {
        string viewName = isNew ? "Create" : "Edit";
        if (!ModelState.IsValid) return View(viewName, book);

        string? previousFile = book.DataFile;
        string? uploadedFile = null;
        try
        {
            uploadedFile = await _service.UploadAsync(file);
            if (uploadedFile is not null) book.DataFile = uploadedFile;

            if (isNew) _service.Add(book);
            else if (!_service.Update(book))
            {
                _service.TryDeleteUpload(uploadedFile);
                return NotFound();
            }

            TempData["Message"] = isNew ? "Đã thêm sách." : "Đã cập nhật sách.";
            return RedirectToAction(nameof(Index));
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError("file", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _service.TryDeleteUpload(uploadedFile);
            _logger.LogError(ex, "Không lưu được sách.");
            ModelState.AddModelError("", "Không lưu được dữ liệu. Hãy kiểm tra quyền ghi thư mục Data.");
        }

        book.DataFile = previousFile;
        return View(viewName, book);
    }

    public IActionResult Read(int id)
    {
        var book = _service.Get(id);
        if (book is null || string.IsNullOrWhiteSpace(book.DataFile)) return NotFound();
        if (!string.Equals(Path.GetExtension(book.DataFile), ".pdf",
            StringComparison.OrdinalIgnoreCase)) return NotFound();

        string path = _service.GetUploadPath(book.DataFile);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "application/pdf", $"sach-{book.Id}.pdf");
    }

    public IActionResult Delete(int id)
    {
        var book = _service.Get(id);
        return book is null ? NotFound() : View(book);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteConfirmed([FromRoute] int id)
    {
        try
        {
            if (!_service.Delete(id)) return NotFound();
            TempData["Message"] = "Đã xóa thông tin sách.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Không xóa được thông tin sách.");
            ModelState.AddModelError("", "Không lưu được thay đổi. Hãy kiểm tra quyền ghi thư mục Data.");
            var book = _service.Get(id);
            return book is null ? NotFound() : View("Delete", book);
        }
    }
}
