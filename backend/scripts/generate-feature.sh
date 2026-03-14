#!/bin/bash

DEFAULT_PATH="./src/features"

read -p "Enter feature name: " FEATURE

FEATURE=$(echo "$FEATURE" | tr '[:upper:]' '[:lower:]')
FEATURE_CAP="${FEATURE^}"

read -p "Is this a CRUD feature? (y/N): " IS_CRUD
IS_CRUD=${IS_CRUD:-N}
IS_CRUD=$(echo "$IS_CRUD" | tr '[:lower:]' '[:upper:]')

FEATURE_PATH="$DEFAULT_PATH/$FEATURE"
BASE_NAMESPACE="backend.src.features.$FEATURE"

# Create folders
mkdir -p "$FEATURE_PATH/controller"
mkdir -p "$FEATURE_PATH/service"
mkdir -p "$FEATURE_PATH/repository"
mkdir -p "$FEATURE_PATH/dto"
mkdir -p "$FEATURE_PATH/entity"
mkdir -p "$FEATURE_PATH/interfaces"

echo "Creating feature in $FEATURE_PATH"

if [ "$IS_CRUD" = "Y" ]; then
    echo "Generating full CRUD boilerplate..."
    SCRIPTS_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
    
    source "$SCRIPTS_DIR/crud/controller.sh"
    source "$SCRIPTS_DIR/crud/service.sh"
    source "$SCRIPTS_DIR/crud/repository.sh"
    source "$SCRIPTS_DIR/crud/dto.sh"
    source "$SCRIPTS_DIR/crud/entity.sh"
    source "$SCRIPTS_DIR/crud/interfaces.sh"
    source "$SCRIPTS_DIR/crud/mapper.sh"
    source "$SCRIPTS_DIR/crud/module.sh"
    
else
    echo "Generating basic feature skeleton..."

############################
# Controller
############################

cat <<EOF > "$FEATURE_PATH/controller/$FEATURE.controller.cs"
using Microsoft.AspNetCore.Mvc;
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.controller;

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
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.service;

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
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.repository;

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
namespace $BASE_NAMESPACE.dto;

public class ${FEATURE_CAP}Dto
{
}
EOF

############################
# Entity
############################

cat <<EOF > "$FEATURE_PATH/entity/$FEATURE.entity.cs"
using System.ComponentModel.DataAnnotations;

namespace $BASE_NAMESPACE.entity;

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
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.interfaces;

public interface I${FEATURE_CAP}Service
{
    Task<List<${FEATURE_CAP}Entity>> GetAll();
}
EOF

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.repository.cs"
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.interfaces;

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
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.service;
using $BASE_NAMESPACE.repository;

namespace $BASE_NAMESPACE;

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

fi

echo "Feature '$FEATURE' created successfully."