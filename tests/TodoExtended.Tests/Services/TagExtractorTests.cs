using TodoExtended.Web.Services;

namespace TodoExtended.Tests.Services;

public class TagExtractorTests
{
    // ── ExtractTags ───────────────────────────────────────────────────────────

    [Fact]
    public void ExtractTags_EmptyTitle_ReturnsEmpty()
    {
        Assert.Empty(TagExtractor.ExtractTags(""));
    }

    [Fact]
    public void ExtractTags_NullLikeWhitespace_ReturnsEmpty()
    {
        Assert.Empty(TagExtractor.ExtractTags("   "));
    }

    [Fact]
    public void ExtractTags_NoTags_ReturnsEmpty()
    {
        Assert.Empty(TagExtractor.ExtractTags("Buy milk and bread"));
    }

    [Fact]
    public void ExtractTags_TagAtEnd_ReturnsTag()
    {
        var tags = TagExtractor.ExtractTags("Buy milk #shopping");
        Assert.Single(tags);
        Assert.Equal("shopping", tags[0]);
    }

    [Fact]
    public void ExtractTags_TagFollowedBySpace_ReturnsTag()
    {
        var tags = TagExtractor.ExtractTags("Buy milk #shopping today");
        Assert.Single(tags);
        Assert.Equal("shopping", tags[0]);
    }

    [Fact]
    public void ExtractTags_MultipleTags_ReturnsAllTags()
    {
        var tags = TagExtractor.ExtractTags("Buy milk #shopping #errand today");
        Assert.Equal(2, tags.Count);
        Assert.Contains("shopping", tags);
        Assert.Contains("errand", tags);
    }

    [Fact]
    public void ExtractTags_TagsAreLowercased()
    {
        var tags = TagExtractor.ExtractTags("Task #Work #HOME");
        Assert.Contains("work", tags);
        Assert.Contains("home", tags);
    }

    [Fact]
    public void ExtractTags_DuplicateTags_ReturnsDistinct()
    {
        var tags = TagExtractor.ExtractTags("Do stuff #work and more #work");
        Assert.Single(tags);
        Assert.Equal("work", tags[0]);
    }

    [Fact]
    public void ExtractTags_TagWithUnderscoreAndDigits_Extracted()
    {
        var tags = TagExtractor.ExtractTags("Task #work_2025");
        Assert.Single(tags);
        Assert.Equal("work_2025", tags[0]);
    }

    [Fact]
    public void ExtractTags_HashWithoutWord_NotExtracted()
    {
        // A lone '#' or '# ' should not produce a tag
        Assert.Empty(TagExtractor.ExtractTags("Task # something"));
    }

    [Fact]
    public void ExtractTags_OnlyTag_ReturnsTag()
    {
        var tags = TagExtractor.ExtractTags("#urgent");
        Assert.Single(tags);
        Assert.Equal("urgent", tags[0]);
    }

    // ── ExtractTagsString ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractTagsString_NoTags_ReturnsNull()
    {
        Assert.Null(TagExtractor.ExtractTagsString("No tags here"));
    }

    [Fact]
    public void ExtractTagsString_OnTag_ReturnsTagName()
    {
        Assert.Equal("work", TagExtractor.ExtractTagsString("Do #work"));
    }

    [Fact]
    public void ExtractTagsString_MultipleTags_ReturnsSpaceSeparated()
    {
        var result = TagExtractor.ExtractTagsString("Task #alpha #beta");
        Assert.NotNull(result);
        var parts = result!.Split(' ');
        Assert.Equal(2, parts.Length);
        Assert.Contains("alpha", parts);
        Assert.Contains("beta", parts);
    }

    // ── ParseTags ─────────────────────────────────────────────────────────────

    [Fact]
    public void ParseTags_Null_ReturnsEmpty()
    {
        Assert.Empty(TagExtractor.ParseTags(null));
    }

    [Fact]
    public void ParseTags_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(TagExtractor.ParseTags(""));
    }

    [Fact]
    public void ParseTags_SpaceSeparated_ReturnsList()
    {
        var tags = TagExtractor.ParseTags("work home");
        Assert.Equal(2, tags.Count);
        Assert.Contains("work", tags);
        Assert.Contains("home", tags);
    }

    [Fact]
    public void ParseTags_SingleTag_ReturnsSingleItem()
    {
        var tags = TagExtractor.ParseTags("urgent");
        Assert.Single(tags);
        Assert.Equal("urgent", tags[0]);
    }
}
