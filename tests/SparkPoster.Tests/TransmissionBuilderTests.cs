namespace SparkPoster.Tests;

public sealed class TransmissionBuilderTests
{
    [Fact]
    public void Build_throws_without_sender()
    {
        var builder = Transmission.Create().To("user@example.com").Html("<p>hi</p>");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("sender", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_throws_without_recipients()
    {
        var builder = Transmission.Create().From("noreply@example.com").Html("<p>hi</p>");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("recipients", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_throws_without_content()
    {
        var builder = Transmission.Create().From("noreply@example.com").To("user@example.com");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("content", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Blank_address_is_rejected_immediately()
    {
        Assert.Throws<ArgumentException>(() => Transmission.Create().From(" "));
        Assert.Throws<ArgumentException>(() => Transmission.Create().To(string.Empty));
    }

    [Fact]
    public void Options_are_omitted_when_none_were_set()
    {
        var request = Transmission.Create()
            .From("noreply@example.com")
            .To("user@example.com")
            .Html("<p>hi</p>")
            .Build();

        Assert.Null(request.Options);
    }

    [Fact]
    public void Build_result_is_reusable_through_with()
    {
        var template = Transmission.Create()
            .From("noreply@example.com")
            .To("first@example.com")
            .Html("<p>hi</p>")
            .Build();

        var second = template with
        {
            Recipients = RecipientSet.Inline([new Recipient { Address = new Address { Email = "second@example.com" } }]),
        };

        Assert.Equal("first@example.com", template.Recipients.Items!.Single().Address.Email);
        Assert.Equal("second@example.com", second.Recipients.Items!.Single().Address.Email);
        Assert.Same(template.Content, second.Content);
    }

    [Fact]
    public void Headers_and_recipient_tags_reach_the_request()
    {
        var request = Transmission.Create()
            .From("noreply@example.com")
            .To(new Recipient
            {
                Address = new Address { Email = "user@example.com", Name = "User" },
                Tags = ["vip"],
            })
            .Header("X-Campaign", "spring")
            .Html("<p>hi</p>")
            .Build();

        Assert.Equal("vip", request.Recipients.Items!.Single().Tags!.Single());
        Assert.Equal("spring", request.Content.Headers!["X-Campaign"]);
    }
}
