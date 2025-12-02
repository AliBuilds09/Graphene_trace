using System.Collections.ObjectModel;
using MyProject.Models;

namespace MyProject.Controllers
{
    public static class CommentsController
    {
        public static ObservableCollection<Comment> Comments { get; } = new();

        public static void AddComment(string author, string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Comments.Add(new Comment
            {
                Author = author,
                Role = role,
                Text = text,
                Timestamp = System.DateTime.Now
            });
        }

        public static void AddReply(Comment comment, string reply)
        {
            if (comment == null || string.IsNullOrWhiteSpace(reply)) return;
            // Only Clinician or Admin may reply to comments
            if (!MyProject.Models.Session.IsClinician && !MyProject.Models.Session.IsAdmin)
            {
                throw new System.UnauthorizedAccessException("Only Clinician or Admin can reply to comments.");
            }
            comment.Reply = reply;
        }
    }
}