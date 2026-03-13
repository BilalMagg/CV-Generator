#!/bin/bash

DEFAULT_PATH="../src/features"

read -p "Enter feature name: " FEATURE

FEATURE=$(echo "$FEATURE" | tr '[:upper:]' '[:lower:]')

FEATURE_PATH="$DEFAULT_PATH/$FEATURE"

mkdir -p "$FEATURE_PATH/interfaces"

echo "Creating feature in $FEATURE_PATH"

############################
# Controller
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.controller.cs"
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/$FEATURE")]
public class ${FEATURE^}Controller : ControllerBase
{
    private readonly I${FEATURE^}Service _service;

    public ${FEATURE^}Controller(I${FEATURE^}Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(result);
    }
}
EOF

############################
# Service
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.service.cs"
public class ${FEATURE^}Service : I${FEATURE^}Service
{
    private readonly I${FEATURE^}Repository _repository;

    public ${FEATURE^}Service(I${FEATURE^}Repository repository)
    {
        _repository = repository;
    }

    public async Task<List<${FEATURE^}Entity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
EOF

############################
# Repository
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.repository.cs"
using Microsoft.EntityFrameworkCore;

public class ${FEATURE^}Repository : I${FEATURE^}Repository
{
    private readonly AppDbContext _context;

    public ${FEATURE^}Repository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<${FEATURE^}Entity>> GetAll()
    {
        return await _context.Set<${FEATURE^}Entity>().ToListAsync();
    }
}
EOF

############################
# DTO
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.dto.cs"
public class ${FEATURE^}Dto
{
}
EOF

############################
# Entity
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.entity.cs"
using System.ComponentModel.DataAnnotations;

public class ${FEATURE^}Entity
{
    [Key]
    public int Id { get; set; }
}
EOF

############################
# Interfaces
############################

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.service.cs"
public interface I${FEATURE^}Service
{
    Task<List<${FEATURE^}Entity>> GetAll();
}
EOF

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.repository.cs"
public interface I${FEATURE^}Repository
{
    Task<List<${FEATURE^}Entity>> GetAll();
}
EOF

############################
# Module Registration
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.module.cs"
using Microsoft.Extensions.DependencyInjection;

public static class ${FEATURE^}Module
{
    public static IServiceCollection Add${FEATURE^}Module(this IServiceCollection services)
    {
        services.AddScoped<I${FEATURE^}Service, ${FEATURE^}Service>();
        services.AddScoped<I${FEATURE^}Repository, ${FEATURE^}Repository>();

        return services;
    }
}
EOF

echo "Feature '$FEATURE' created successfully."