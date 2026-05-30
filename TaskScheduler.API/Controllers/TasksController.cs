using Microsoft.AspNetCore.Mvc;
using TaskScheduler.Core.DTOs;
using TaskScheduler.Core.Entities;
using TaskScheduler.Core.Interfaces;

namespace TaskScheduler.API.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ITaskService taskService, ILogger<TasksController> logger)
    {
        _taskService = taskService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new scheduled task and registers it with the scheduler.
    /// System-managed jobs (MasterServerSyncJob, SymbolDataPullJob) cannot be created manually.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // System jobs are managed automatically — block manual creation
        var systemJobs = new[] { JobType.MasterServerSyncJob, JobType.SymbolDataPullJob };

        if (systemJobs.Contains(request.JobType))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { error = $"'{request.JobType}' is a system-managed job and cannot be created manually." });

        try
        {
            var result = await _taskService.CreateTaskAsync(request);
            return CreatedAtAction(nameof(GetTaskById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Returns a paginated list of tasks. Supports filtering by job type and enabled state.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? jobType = null,
        [FromQuery] bool? isEnabled = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var result = await _taskService.GetTasksAsync(page, pageSize, jobType, isEnabled);
        return Ok(result);
    }

    /// <summary>
    /// Returns a single task by its ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskById(string id)
    {
        var result = await _taskService.GetTaskByIdAsync(id);
        if (result == null)
            return NotFound(new { error = $"Task '{id}' not found." });

        return Ok(result);
    }

    /// <summary>
    /// Returns the current execution status and history of a task.
    /// </summary>
    [HttpGet("{id}/status")]
    [ProducesResponseType(typeof(TaskStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTaskStatus(string id)
    {
        var result = await _taskService.GetTaskStatusAsync(id);
        if (result == null)
            return NotFound(new { error = $"Task '{id}' not found." });

        return Ok(result);
    }

    /// <summary>
    /// Immediately triggers a task outside of its normal schedule.
    /// </summary>
    [HttpPost("{id}/trigger")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TriggerTask(string id)
    {
        var triggered = await _taskService.TriggerTaskAsync(id);
        if (!triggered)
            return NotFound(new { error = $"Task '{id}' not found or not scheduled." });

        return Ok(new { message = $"Task '{id}' triggered successfully." });
    }

    /// <summary>
    /// Updates an existing task. Only provided fields are changed.
    /// If the schedule is updated, the job is automatically rescheduled.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TaskResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTask(string id, [FromBody] UpdateTaskRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _taskService.UpdateTaskAsync(id, request);
            if (result == null)
                return NotFound(new { error = $"Task '{id}' not found." });

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a task and removes it from the scheduler.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTask(string id)
    {
        var deleted = await _taskService.DeleteTaskAsync(id);
        if (!deleted)
            return NotFound(new { error = $"Task '{id}' not found." });

        return Ok(new { message = $"Task '{id}' deleted successfully." });
    }
}
