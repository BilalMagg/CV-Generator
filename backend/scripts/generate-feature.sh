#!/bin/bash

DEFAULT_PATH="./src/features"

read -p "Enter feature name: " FEATURE

FEATURE=$(echo "$FEATURE" | tr '[:upper:]' '[:lower:]')
FEATURE_CAP="${FEATURE^}"  # capitalized for class names

FEATURE_PATH="$DEFAULT_PATH/$FEATURE"

# Create folders
mkdir -p "$FEATURE_PATH/controller"
mkdir -p "$FEATURE_PATH/service"
mkdir -p "$FEATURE_PATH/repository"
mkdir -p "$FEATURE_PATH/dto"
mkdir -p "$FEATURE_PATH/entity"
mkdir -p "$FEATURE_PATH/interfaces"

echo "Creating feature in $FEATURE_PATH"

############################
# Controller
############################

cat <<EOF > "$FEATURE_PATH/controller/$FEATURE.controller.cs"
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/$FEATURE")]
public class ${FEATURE_CAP}Controller : ControllerBase
{
    private readonly I${FEATURE_CAP}Service _service;

    public ${FEATURE_CAP}Controller(I${FEATURE_CAP}Service service)
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

cat <<EOF > "$FEATURE_PATH/service/$FEATURE.service.cs"
public class ${FEATURE_CAP}Service : I${FEATURE_CAP}Service
{
    private readonly I${FEATURE_CAP}Repository _repository;

    public ${FEATURE_CAP}Service(I${FEATURE_CAP}Repository repository)
    {
        _repository = repository;
    }

    public async Task<List<${FEATURE_CAP}Entity>> GetAll()
    {
        return await _repository.GetAll();
    }
}
EOF

############################
# Repository
############################

cat <<EOF > "$FEATURE_PATH/repository/$FEATURE.repository.cs"
using Microsoft.EntityFrameworkCore;

public class ${FEATURE_CAP}Repository : I${FEATURE_CAP}Repository
{
    private readonly AppDbContext _context;

    public ${FEATURE_CAP}Repository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<${FEATURE_CAP}Entity>> GetAll()
    {
        return await _context.Set<${FEATURE_CAP}Entity>().ToListAsync();
    }
}
EOF

############################
# DTO
############################

cat <<EOF > "$FEATURE_PATH/dto/$FEATURE.dto.cs"
public class ${FEATURE_CAP}Dto
{
}
EOF

############################
# Entity
############################

cat <<EOF > "$FEATURE_PATH/entity/$FEATURE.entity.cs"
using System.ComponentModel.DataAnnotations;

public class ${FEATURE_CAP}Entity
{
    [Key]
    public int Id { get; set; }
}
EOF

############################
# Interfaces
############################

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.service.cs"
public interface I${FEATURE_CAP}Service
{
    Task<List<${FEATURE_CAP}Entity>> GetAll();
}
EOF

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.repository.cs"
public interface I${FEATURE_CAP}Repository
{
    Task<List<${FEATURE_CAP}Entity>> GetAll();
}
EOF

############################
# Module Registration
############################

cat <<EOF > "$FEATURE_PATH/$FEATURE.module.cs"
using Microsoft.Extensions.DependencyInjection;

public static class ${FEATURE_CAP}Module
{
    public static IServiceCollection Add${FEATURE_CAP}Module(this IServiceCollection services)
    {
        services.AddScoped<I${FEATURE_CAP}Service, ${FEATURE_CAP}Service>();
        services.AddScoped<I${FEATURE_CAP}Repository, ${FEATURE_CAP}Repository>();

        return services;
    }
}
EOF

echo "Feature '$FEATURE' created successfully."