namespace SparkPoster.Tests;

public sealed class TransmissionBuilderTests
{
    [Fact]
    public void Без_отправителя_Build_бросает()
    {
        var builder = Transmission.Create().To("user@example.com").Html("<p>hi</p>");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("отправитель", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Без_получателей_Build_бросает()
    {
        var builder = Transmission.Create().From("noreply@example.com").Html("<p>hi</p>");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("получател", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Без_содержимого_Build_бросает()
    {
        var builder = Transmission.Create().From("noreply@example.com").To("user@example.com");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("содержимое", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Пустой_адрес_отвергается_сразу()
    {
        Assert.Throws<ArgumentException>(() => Transmission.Create().From(" "));
        Assert.Throws<ArgumentException>(() => Transmission.Create().To(string.Empty));
    }

    [Fact]
    public void Без_заданных_опций_options_не_попадает_в_запрос()
    {
        var request = Transmission.Create()
            .From("noreply@example.com")
            .To("user@example.com")
            .Html("<p>hi</p>")
            .Build();

        Assert.Null(request.Options);
    }

    [Fact]
    public void Результат_Build_переиспользуется_через_with()
    {
        var template = Transmission.Create()
            .From("noreply@example.com")
            .To("first@example.com")
            .Html("<p>hi</p>")
            .Build();

        var second = template with
        {
            Recipients = [new Recipient { Address = new Address { Email = "second@example.com" } }],
        };

        Assert.Equal("first@example.com", template.Recipients.Single().Address.Email);
        Assert.Equal("second@example.com", second.Recipients.Single().Address.Email);
        Assert.Same(template.Content, second.Content);
    }

    [Fact]
    public void Заголовки_и_метки_получателя_доходят_до_запроса()
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

        Assert.Equal("vip", request.Recipients.Single().Tags!.Single());
        Assert.Equal("spring", request.Content.Headers!["X-Campaign"]);
    }
}
