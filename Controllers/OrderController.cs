using Microsoft.AspNetCore.Mvc;
using track_api.Services;

namespace track_api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class JobController : ControllerBase
    {
        private readonly GetTrackService _getTrackService;

        public JobController(GetTrackService getTrackService)
        {
            _getTrackService = getTrackService;
        }


        [HttpGet("{orderId}")]
        public IActionResult GetJobByOrderId(int orderId)
        {
            var job = _getTrackService.GetJobByOrderId(orderId);
            if (job == null)
            {
                return NotFound($"Job with OrderId: {orderId} not found.");
            }
            return Ok(job);
        }
    }
}
