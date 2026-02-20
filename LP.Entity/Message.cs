using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LP.Entity
{
    public class Message : BaseEntity
    {
        public Guid ChatId { get; set; }
        public Guid UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime Time { get; set; } = DateTime.UtcNow;
        [StringLength(10)]
        public string Status { get; set; } = "delivered";
        // 0=обычное, 1=системное, 2=бот-вопрос, 3=ответ-на-бота
        public int MessageType { get; set; } = 0;
    }
}