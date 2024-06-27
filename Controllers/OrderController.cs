using Microsoft.AspNetCore.Mvc;

namespace track_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobController : ControllerBase
    {
        private readonly JobService _jobService;

        public JobController(JobService jobService)
        {
            _jobService = jobService;
        }


        [HttpGet("{orderId}")]
        public IActionResult GetJobByOrderId(int orderId)
        {
            var job = _jobService.GetJobByOrderId(orderId);
            if (job == null)
            {
                return NotFound($"Job with OrderId: {orderId} not found.");
            }
            return Ok(job);
        }
    }
}
