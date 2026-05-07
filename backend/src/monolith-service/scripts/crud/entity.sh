cat <<EOF > "$FEATURE_PATH/entity/$FEATURE.entity.cs"
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace $BASE_NAMESPACE.entity
{
    [Table("${FEATURE}s")]
    public class ${FEATURE_CAP}
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
EOF
