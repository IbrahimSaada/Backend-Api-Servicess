namespace Backend_Api_services.Models.DTOs
{
    public class PaginatedCommentResponse
    {
        public List<CommentResponse> Comments { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
