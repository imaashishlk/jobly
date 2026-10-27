using Jobly.Api.Features.Cvs;

namespace Jobly.Api.UnitTests.Features.Cvs;

public sealed class CvTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_UploadedFile_KeepsFileNameAndTrimsText()
    {
        // Act
        var result = Cv.Create("  Ola Nordmann\n.NET developer  ", CvSource.Uploaded, " cv.pdf ", Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Ola Nordmann\n.NET developer", result.Value.Text);
        Assert.Equal("cv.pdf", result.Value.FileName);
        Assert.Equal(CvSource.Uploaded, result.Value.Source);
        Assert.Equal(Now, result.Value.UpdatedAt);
    }

    [Fact]
    public void Create_PastedText_HasNoFileName()
    {
        // Act
        var result = Cv.Create("My CV text", CvSource.Pasted, null, Now);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.FileName);
        Assert.Equal(CvSource.Pasted, result.Value.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  ")]
    public void Create_TextIsEmpty_ReturnsTextRequired(string text)
    {
        // Act
        var result = Cv.Create(text, CvSource.Pasted, null, Now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TextRequired", result.Error.Code);
    }

    [Fact]
    public void Create_TextIsExactlyTheLimit_Succeeds()
    {
        // Act
        var result = Cv.Create(new string('a', Cv.MaxTextLength), CvSource.Pasted, null, Now);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_TextIsOneCharacterOverTheLimit_ReturnsTooLong()
    {
        // Act
        var result = Cv.Create(new string('a', Cv.MaxTextLength + 1), CvSource.Pasted, null, Now);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TooLong", result.Error.Code);
    }

    [Fact]
    public void ReplaceText_ValidText_ChangesTextAndKeepsFileNameAndSource()
    {
        // Arrange
        var cv = Cv.Create("Old text", CvSource.Uploaded, "cv.docx", Now).Value;

        // Act: the user fixed the extracted text by hand.
        var result = cv.ReplaceText("  New text ", Now.AddHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("New text", cv.Text);
        Assert.Equal("cv.docx", cv.FileName);
        Assert.Equal(CvSource.Uploaded, cv.Source);
        Assert.Equal(Now.AddHours(1), cv.UpdatedAt);
    }

    [Fact]
    public void ReplaceText_TextIsEmpty_ReturnsTextRequiredAndKeepsOldText()
    {
        // Arrange
        var cv = Cv.Create("Old text", CvSource.Pasted, null, Now).Value;

        // Act
        var result = cv.ReplaceText("   ", Now.AddHours(1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TextRequired", result.Error.Code);
        Assert.Equal("Old text", cv.Text);
        Assert.Equal(Now, cv.UpdatedAt);
    }

    [Fact]
    public void ReplaceText_TextIsTooLong_ReturnsTooLongAndKeepsOldText()
    {
        // Arrange
        var cv = Cv.Create("Old text", CvSource.Pasted, null, Now).Value;

        // Act
        var result = cv.ReplaceText(new string('a', Cv.MaxTextLength + 1), Now.AddHours(1));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Cv.TooLong", result.Error.Code);
        Assert.Equal("Old text", cv.Text);
    }
}
