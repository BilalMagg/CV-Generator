cat <<EOF > "$FEATURE_PATH/dto/$FEATURE.dto.cs"
namespace $BASE_NAMESPACE.dto;

public class Create${FEATURE_CAP}Dto
{
}

public class Update${FEATURE_CAP}Dto
{
}

public class ${FEATURE_CAP}ResponseDto
{
    public Guid Id { get; set; }
}
EOF
