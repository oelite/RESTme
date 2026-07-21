using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace OElite.Restme.ClickHouse
{
    /// <summary>
    /// Translates LINQ expressions to ClickHouse SQL WHERE clauses
    /// </summary>
    public class ClickHouseExpressionTranslator : ExpressionVisitor
    {
        private readonly StringBuilder _sql = new StringBuilder();
        private readonly Dictionary<string, object> _parameters = new Dictionary<string, object>();

        public string Translate<T>(Expression<Func<T, bool>> expression)
        {
            Visit(expression.Body);
            return _sql.ToString();
        }

        public Dictionary<string, object> GetParameters() => _parameters;

        protected override Expression VisitBinary(BinaryExpression node)
        {
            _sql.Append("(");
            Visit(node.Left);

            switch (node.NodeType)
            {
                case ExpressionType.Equal:
                    _sql.Append(" = ");
                    break;
                case ExpressionType.NotEqual:
                    _sql.Append(" != ");
                    break;
                case ExpressionType.GreaterThan:
                    _sql.Append(" > ");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    _sql.Append(" >= ");
                    break;
                case ExpressionType.LessThan:
                    _sql.Append(" < ");
                    break;
                case ExpressionType.LessThanOrEqual:
                    _sql.Append(" <= ");
                    break;
                case ExpressionType.AndAlso:
                    _sql.Append(" AND ");
                    break;
                case ExpressionType.OrElse:
                    _sql.Append(" OR ");
                    break;
                default:
                    throw new NotSupportedException($"Binary operator {node.NodeType} is not supported");
            }

            Visit(node.Right);
            _sql.Append(")");

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression)
            {
                // Convert property name to ClickHouse column name (snake_case)
                var columnName = ToSnakeCase(node.Member.Name);
                _sql.Append(columnName);
            }
            else if (node.Expression is MemberExpression memberExpr)
            {
                // Handle nested property access
                Visit(memberExpr);
                _sql.Append(".");
                _sql.Append(node.Member.Name.ToLowerInvariant());
            }
            else
            {
                throw new NotSupportedException($"Member access {node} is not supported");
            }

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            var paramName = $"@p{_parameters.Count}";
            _parameters[paramName] = node.Value;
            _sql.Append(paramName);
            return node;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(string))
            {
                HandleStringMethod(node);
            }
            else if (node.Method.Name == "Contains")
            {
                HandleContainsMethod(node);
            }
            else
            {
                throw new NotSupportedException($"Method {node.Method.Name} is not supported");
            }

            return node;
        }

        private void HandleStringMethod(MethodCallExpression node)
        {
            _sql.Append("(");
            Visit(node.Object);

            switch (node.Method.Name)
            {
                case "Contains":
                    _sql.Append(" LIKE ");
                    var paramName = $"@p{_parameters.Count}";
                    _parameters[paramName] = $"%{GetConstantValue(node.Arguments[0])}%";
                    _sql.Append(paramName);
                    break;
                case "StartsWith":
                    _sql.Append(" LIKE ");
                    paramName = $"@p{_parameters.Count}";
                    _parameters[paramName] = $"{GetConstantValue(node.Arguments[0])}%";
                    _sql.Append(paramName);
                    break;
                case "EndsWith":
                    _sql.Append(" LIKE ");
                    paramName = $"@p{_parameters.Count}";
                    _parameters[paramName] = $"%{GetConstantValue(node.Arguments[0])}";
                    _sql.Append(paramName);
                    break;
                default:
                    throw new NotSupportedException($"String method {node.Method.Name} is not supported");
            }
            _sql.Append(")");
        }

        private void HandleContainsMethod(MethodCallExpression node)
        {
            if (node.Method.DeclaringType == typeof(Enumerable))
            {
                // Handle array.Contains(item)
                var paramName = $"@p{_parameters.Count}";
                _parameters[paramName] = GetConstantValue(node.Arguments[0]);
                _sql.Append(paramName);

                _sql.Append(" IN (");
                Visit(node.Arguments[1]);
                _sql.Append(")");
            }
            else
            {
                throw new NotSupportedException("Contains method is only supported for arrays");
            }
        }

        private object GetConstantValue(Expression expression)
        {
            if (expression is ConstantExpression constant)
            {
                return constant.Value;
            }
            else if (expression is MemberExpression member && member.Expression is ConstantExpression constExpr)
            {
                var container = constExpr.Value;
                var field = member.Member as FieldInfo;
                return field?.GetValue(container);
            }

            throw new NotSupportedException($"Cannot extract constant value from {expression}");
        }

        private static string ToSnakeCase(string pascalCase)
        {
            if (string.IsNullOrEmpty(pascalCase))
                return pascalCase;

            var result = new StringBuilder();
            for (int i = 0; i < pascalCase.Length; i++)
            {
                char currentChar = pascalCase[i];
                if (char.IsUpper(currentChar) && i > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(currentChar));
            }
            return result.ToString();
        }
    }
}