using Microsoft.AspNetCore.Mvc;
using backend.src.features.user.interfaces;
using backend.src.features.user.dto;

namespace backend.src.features.user.controller;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IUserService _service;

    public UserController(IUserService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _service.GetAll();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _service.GetById(id);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        var user = await _service.Create(dto);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserDto dto)
    {
        var user = await _service.Update(id, dto);

        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.Delete(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}