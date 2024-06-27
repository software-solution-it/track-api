namespace track_api.Services
{
    public class GetTrackService
    {
        private Timer _timer;
        private readonly ILogger<JobService> _logger;
        private readonly IServiceProvider _services;

        public GetTrackService(IServiceProvider services, ILogger<JobService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public ValidationPost GetJobByOrderId(int orderId)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MyDbContext>();
            return context.ValidationPost.FirstOrDefault(job => job.OrderId == orderId);
        }
    }
}
