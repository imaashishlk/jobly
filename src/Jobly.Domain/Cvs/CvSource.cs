namespace Jobly.Domain.Cvs;

/// <summary>How the CV text got into the app.</summary>
public enum CvSource
{
    /// <summary>The text was read from an uploaded PDF or DOCX file.</summary>
    Uploaded,

    /// <summary>The user pasted or typed the text.</summary>
    Pasted,
}
