using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using track_api.Models;
using static System.Reflection.Metadata.BlobBuilder;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

public class JobService : IHostedService, IDisposable
{
    private Timer _timer;
    private readonly ILogger<JobService> _logger;
    private readonly IServiceProvider _services;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public JobService(IServiceProvider services, ILogger<JobService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JobService is starting.");
        StartJob();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("JobService is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public void StartJob()
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(4));
    }

    private async void DoWork(object state)
    {
        if (!await _semaphore.WaitAsync(0))
        {
            _logger.LogInformation("DoWork is already running. Skipping this interval.");
            return;
        }

        try
        {
            _logger.LogInformation("JobService is working.");

            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            var orders = await FetchOrdersAsync(context);

            foreach (var order in orders)
            {
                await ProcessOrderAsync(context, order);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Order>> FetchOrdersAsync(MyDbContext context)
    {
        return await context.Order
            .FromSqlRaw(@"
                SELECT
                    p.ID AS OrderId,
                    p.post_date AS OrderDate,
                    pm1.meta_value AS TrackingCode,
                    oim.order_item_name AS ItemName,
                    u.user_email AS UserEmail,
                    u.display_name AS UserName
                FROM
                    wp_posts AS p
                JOIN
                    wp_postmeta AS pm1 ON p.ID = pm1.post_id
                JOIN
                    wp_woocommerce_order_items AS oim ON p.ID = oim.order_id
                JOIN
                    wp_woocommerce_order_itemmeta AS oim_meta ON oim.order_item_id = oim_meta.order_item_id
                JOIN
                    wp_postmeta AS pm2 ON p.ID = pm2.post_id AND pm2.meta_key = '_customer_user'
                JOIN
                    wp_users AS u ON pm2.meta_value = u.ID
                LEFT JOIN
                    wp_validation_post AS vp ON p.ID = vp.order_id
                WHERE
                    p.post_type = 'shop_order'
                    AND p.post_status IN ('wc-processing', 'wc-completed')
                    AND pm1.meta_key = '_correios_tracking_code'
                    AND oim_meta.meta_key = '_product_id'
                    AND (vp.completed IS NULL OR vp.completed = 0)
                ORDER BY
                    p.post_date DESC")
            .ToListAsync();
    }

    private async Task ProcessOrderAsync(MyDbContext context, Order order)
    {
        if (order == null || string.IsNullOrEmpty(order.TrackingCode))
        {
            _logger.LogError("Order or TrackingCode is null.");
            return;
        }

        order.TrackingCode = order.TrackingCode?.ToUpperInvariant();
        if (order.TrackingCode?.StartsWith("NM") == false)
        {
            var validationPost = await context.ValidationPost.FirstOrDefaultAsync(vp => vp.OrderId == order.OrderId);

            if (validationPost == null)
            {
                await CreateValidationPostWithStatusAsync(context, order, "Pedido em processo de entrega", 1, 4);
            }
            else
            {
                validationPost.SendCount = 4;
                validationPost.Completed = 1;
                validationPost.UpdatedAt = DateTime.Now;
                validationPost.PostMessage = "Pedido em processo de entrega";
                await context.SaveChangesAsync();
            }
            return;
        }

        var existingValidationPost = await context.ValidationPost.FirstOrDefaultAsync(vp => vp.OrderId == order.OrderId);

        if (existingValidationPost != null && existingValidationPost.Completed == 1 && existingValidationPost.SendCount < 4)
        {
            existingValidationPost.SendCount = 4;
            await context.SaveChangesAsync();
        }

        var sendEmail = await ShouldSendEmailAsync(existingValidationPost);

        if (sendEmail)
        {
            await HandleEmailSendingAsync(context, order, existingValidationPost);
        }
    }

    private async Task CreateValidationPostWithStatusAsync(MyDbContext context, Order order, string statusName, int completed, int sendCount)
    {
        if (await context.ValidationPost.AnyAsync(vp => vp.OrderId == order.OrderId))
        {
            return;
        }

        string emailBody = await GenerateEmailBody(DateTime.Now, statusName, sendCount, order.TrackingCode);
        bool emailSent = SendEmail(order.UserEmail, "Status do seu pedido", emailBody, order.TrackingCode);

        if (emailSent)
        {
            await context.ValidationPost.AddAsync(new ValidationPost
            {
                OrderId = order.OrderId,
                SendCount = sendCount,
                Completed = completed,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                PostMessage = statusName
            });

            _logger.LogWarning("E-mail enviado para " + order.UserEmail);

            await context.SaveChangesAsync();
        }
    }


    private async Task<bool> ShouldSendEmailAsync(ValidationPost validationPost)
    {
        if (validationPost == null)
        {
            return true;
        }

        if (validationPost.SendCount < 4)
        {
            var nextEmailTime = validationPost.UpdatedAt.AddDays(2);
            return DateTime.Now >= nextEmailTime;
        }

        return false;
    }

    private async Task HandleEmailSendingAsync(MyDbContext context, Order order, ValidationPost validationPost)
    {
        var trackingInfo = await GetTrackingInfo(order.TrackingCode);
        var statusName = "Pedido despachado";

        if (trackingInfo?.Eventos?.Count > 0)
        {

            var deliveredStatus = trackingInfo.Eventos.Any(e => e.Status.Contains("Objeto entregue ao destinatário", StringComparison.OrdinalIgnoreCase));
            if (deliveredStatus)
            {
                statusName = "Objeto entregue ao destinatário";
            }

            if (validationPost != null)
            {
                await UpdateValidationPostWithStatusAsync(context, validationPost, order, statusName, deliveredStatus ? 1 : 0);
            }
            else
            {
                await CreateValidationPostWithStatusAsync(context, order, statusName, deliveredStatus ? 1 : 0);
            }
        }
        else
        {
            if (validationPost != null && validationPost?.SendCount < 3)
            {
                statusName = validationPost.SendCount switch
                {
                    1 => "Pedido em centro de distribuição local",
                    2 => "Pedido em triagem aduaneira",
                    _ => statusName
                };

                await UpdateValidationPostAsync(context, validationPost, order, statusName);
            }
            else if (validationPost?.SendCount == null)
            {
                await CreateValidationPostAsync(context, order, statusName);
            }
        }
    }

    private async Task UpdateValidationPostWithStatusAsync(MyDbContext context, ValidationPost validationPost, Order order, string statusName, int completed)
    {
        validationPost.SendCount++;
        validationPost.Completed = completed;
        validationPost.UpdatedAt = DateTime.Now;
        validationPost.PostMessage = statusName;

        string emailBody = await GenerateEmailBody(DateTime.Now, statusName, validationPost.SendCount, order.TrackingCode);
        bool emailSent = SendEmail(order.UserEmail, "Atualização do seu pedido", emailBody, order.TrackingCode);

        if (emailSent)
        {
            await context.SaveChangesAsync();
        }
    }


    private async Task CreateValidationPostWithStatusAsync(MyDbContext context, Order order, string statusName, int completed)
    {
        if (await context.ValidationPost.AnyAsync(vp => vp.OrderId == order.OrderId))
        {
            return;
        }

        string emailBody = await GenerateEmailBody(DateTime.Now, statusName, 4, order.TrackingCode);
        bool emailSent = SendEmail(order.UserEmail, "Status do seu pedido", emailBody, order.TrackingCode);

        if (emailSent)
        {
            await context.ValidationPost.AddAsync(new ValidationPost
            {
                OrderId = order.OrderId,
                SendCount = 4,
                Completed = completed,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                PostMessage = statusName
            });

            _logger.LogWarning("E-mail enviado para " + order.UserEmail);

            await context.SaveChangesAsync();
        }
    }

    private async Task UpdateValidationPostAsync(MyDbContext context, ValidationPost validationPost, Order order, string statusName)
    {
        validationPost.SendCount++;
        validationPost.Completed = 0;
        validationPost.UpdatedAt = DateTime.Now;
        validationPost.PostMessage = statusName;

        string emailBody = await GenerateEmailBody(DateTime.Now, statusName, validationPost.SendCount, order.TrackingCode);
        bool emailSent = SendEmail(order.UserEmail, "Status do seu pedido", emailBody, order.TrackingCode);

        if (emailSent)
        {
            await context.SaveChangesAsync();
        }
    }

    private async Task CreateValidationPostAsync(MyDbContext context, Order order, string statusName)
    {
        if (await context.ValidationPost.AnyAsync(vp => vp.OrderId == order.OrderId))
        {
            return;
        }

        string emailBody = await GenerateEmailBody(DateTime.Now, statusName, 1, order.TrackingCode);
        bool emailSent = SendEmail(order.UserEmail, "Status do seu pedido", emailBody, order.TrackingCode);

        if (emailSent)
        {
            await context.ValidationPost.AddAsync(new ValidationPost
            {
                OrderId = order.OrderId,
                SendCount = 1,
                Completed = 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                PostMessage = statusName
            });

            _logger.LogWarning("E-mail enviado para " + order.UserEmail);

            await context.SaveChangesAsync();
        }
    }

    private async Task<TrackingResponse> GetTrackingInfo(string trackingCode)
    {
        using var client = new HttpClient();
        var url = $"https://api.linketrack.com/track/json?user=teste&token=1abcd00b2731640e886fb41a8a9671ad1434c599dbaa0a0de9a5aa619f29a83f&codigo={trackingCode}";

        while (true)
        {
            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<TrackingResponse>(json);
            }

            _logger.LogWarning("Failed to retrieve tracking info. Retrying in 6 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(6));
        }
    }

    private async Task<string> GenerateEmailBody(DateTime updatedAt, string statusName, int sendingCount, string trackingCode)
    {
        var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", sendingCount == 4 ? "Template.html" : "TemplateWithoutCode.html");

        if (!File.Exists(templatePath))
        {
            _logger.LogError("Template file not found: " + templatePath);
            throw new FileNotFoundException("Template file not found", templatePath);
        }

        string template = await File.ReadAllTextAsync(templatePath);

        return template.Replace("{{UpdatedAt}}", updatedAt.ToString())
                       .Replace("{{SendingCode}}", sendingCount.ToString())
                       .Replace("{{StatusName}}", statusName)
                       .Replace("{{TrackingCode}}", trackingCode);
    }

    private bool SendEmail(string to, string subject, string body, string code)
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
        try
        {
            var from = new MailAddress("contato@useblazee.com.br", "Blazee");
            var toAddress = new MailAddress(to);

            var message = new MailMessage(from, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            using var smtp = new SmtpClient("smtp.hostinger.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("contato@useblazee.com.br", "Blazee@2024")
            };

            smtp.Send(message);
            _logger.LogInformation("Email sent successfully to " + to + " with code " + code);
            return true;
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error occurred while sending email.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while sending email.");
            return false;
        }
    }
}
