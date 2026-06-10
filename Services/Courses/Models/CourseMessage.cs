namespace SkillForge.Services.Courses.Models
{
    public enum CourseMessage
    {
        None,

        //For Instructor
        EmptyFields,
        CourseAdded,
        CourseNotAdded,
        SavedToDraft,
        SentForApproval,
        CoursePublished,
        CourseActive,
        CourseDeleted,
        CourseUpdate,
        thumbnailUploaded,
        thumbnailNotUpload,

        //For Student
        PurchaseSUccess,
        AddWishlist,
        CancellBuy,
        EnrollSucces,
        PurchaseFailed,
        CouponApplied,
        InvalidCoupon
    }
}
