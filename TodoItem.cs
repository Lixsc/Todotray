using System;

namespace IDK
{
    public enum TodoPriority
    {
        Normal = 0,
        Medium = 1,
        High = 2
    }

    public class TodoItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Priority { get; set; } // 0 = Normal, 1 = Medium, 2 = High
        public DateTime? ReminderTime { get; set; }
        public bool HasReminder { get; set; }
        public bool ReminderFired { get; set; }

        public TodoItem()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            Priority = (int)TodoPriority.Normal;
            HasReminder = false;
            ReminderFired = false;
        }

        public TodoItem(string title, TodoPriority priority = TodoPriority.Normal) : this()
        {
            Title = title;
            Priority = (int)priority;
            IsCompleted = false;
        }

        public TodoPriority GetPriority()
        {
            return (TodoPriority)Priority;
        }

        public void SetPriority(TodoPriority p)
        {
            Priority = (int)p;
        }

        public void SetReminder(DateTime reminder)
        {
            ReminderTime = reminder;
            HasReminder = true;
            ReminderFired = false;
        }

        public void ClearReminder()
        {
            ReminderTime = null;
            HasReminder = false;
            ReminderFired = false;
        }

        public string GetReminderBadgeText()
        {
            if (!HasReminder || !ReminderTime.HasValue) return null;

            var diff = ReminderTime.Value - DateTime.Now;
            if (diff.TotalSeconds <= 0)
            {
                return "? Overdue";
            }
            if (diff.TotalHours < 1)
            {
                return string.Format("? {0}m", Math.Max(1, (int)diff.TotalMinutes));
            }
            if (diff.TotalHours < 24)
            {
                return string.Format("? {0:h\\:mm tt}", ReminderTime.Value);
            }
            return string.Format("? {0:MMM dd}", ReminderTime.Value);
        }

        public override string ToString()
        {
            return Title ?? string.Empty;
        }
    }
}

