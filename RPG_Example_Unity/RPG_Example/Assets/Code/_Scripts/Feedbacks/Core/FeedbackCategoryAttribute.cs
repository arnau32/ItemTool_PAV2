using System;

namespace FeedbacksNagu
{
    // Decorates a FeedbackBase subclass to assign it to a named category
    // in the FeedbackContainerEditorWindow palette.
    //
    // Usage:
    //   [FeedbackCategory("Camera")]
    //   public class FeedbackCamaraShakeCinemachine : FeedbackBase { ... }
    //
    // Types without this attribute are grouped under "Other" automatically.
    // New categories appear in the palette without any editor changes — just
    // add the attribute to the feedback class and recompile.
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class FeedbackCategoryAttribute : Attribute
    {
        public string Category { get; }

        public FeedbackCategoryAttribute(string category)
        {
            Category = string.IsNullOrWhiteSpace(category) ? "Other" : category;
        }
    }
}