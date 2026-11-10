using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BookManager.Models;

public class Book
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sách.")]
    [StringLength(200, ErrorMessage = "Tên sách tối đa 200 ký tự.")]
    [Display(Name = "Tên sách")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập tác giả.")]
    [StringLength(200, ErrorMessage = "Tác giả tối đa 200 ký tự.")]
    [Display(Name = "Tác giả")]
    public string Authors { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập nhà xuất bản.")]
    [StringLength(200, ErrorMessage = "Nhà xuất bản tối đa 200 ký tự.")]
    [Display(Name = "Nhà xuất bản")]
    public string Publisher { get; set; } = "";

    [Range(1, 2100, ErrorMessage = "Năm xuất bản phải từ 1 đến 2100.")]
    [Display(Name = "Năm xuất bản")]
    public int Year { get; set; } = DateTime.Now.Year;

    [StringLength(2000, ErrorMessage = "Mô tả tối đa 2000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; set; }

    [BindNever]
    public string? DataFile { get; set; }
}
