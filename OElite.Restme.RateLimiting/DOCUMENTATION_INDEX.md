# OElite.Restme.RateLimiting Documentation Index

## 📚 Complete Documentation Suite

This package includes comprehensive documentation for enterprise-grade rate limiting in ASP.NET Core applications.

### Core Documentation

1. **[README.md](README.md)** - Main package documentation
   - Quick start guide
   - Feature overview
   - Installation instructions
   - Basic configuration examples
   - Performance considerations
   - Security best practices

2. **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)** - Complete API reference
   - All classes, interfaces, and methods
   - Configuration options reference
   - Data models and enums
   - Service implementations
   - Extension methods
   - Middleware components

3. **[EXAMPLES.md](EXAMPLES.md)** - Comprehensive examples
   - Basic setup scenarios
   - Advanced configuration examples
   - Per-endpoint rate limiting
   - DDoS protection implementation
   - Adaptive rate limiting
   - Redis distributed storage
   - Custom storage implementations
   - Testing examples
   - Production deployment
   - Troubleshooting examples

### Migration Documentation

4. **[KORTEX_RATELIMITING_MIGRATION_GUIDE.md](../helios/kortex/KORTEX_RATELIMITING_MIGRATION_GUIDE.md)** - Kortex migration guide
   - Step-by-step migration process
   - Configuration mapping
   - Feature comparison
   - Rollback procedures
   - Testing strategy
   - Timeline and success metrics

## 🚀 Quick Navigation

### Getting Started
- [Installation](README.md#installation)
- [Basic Setup](README.md#basic-setup)
- [Quick Start](README.md#quick-start)

### Configuration
- [Configuration Options](API_DOCUMENTATION.md#ratelimitoptions)
- [Rate Limiting Algorithms](README.md#rate-limiting-algorithms)
- [DDoS Protection](README.md#ddos-protection)
- [Adaptive Rate Limiting](README.md#adaptive-rate-limiting)

### Advanced Features
- [Distributed Storage](README.md#distributed-storage)
- [Per-Endpoint Configuration](EXAMPLES.md#per-endpoint-rate-limiting)
- [Custom Storage](EXAMPLES.md#custom-storage-implementation)
- [Emergency Mode](API_DOCUMENTATION.md#emergencymodeconfig)

### Implementation
- [Service Registration](API_DOCUMENTATION.md#ratelimitingservicecollectionextensions)
- [Middleware Usage](API_DOCUMENTATION.md#ratelimitmiddlewareextensions)
- [Custom Implementations](EXAMPLES.md#custom-storage-implementation)
- [Testing](EXAMPLES.md#testing-examples)

### Production
- [Docker Deployment](EXAMPLES.md#docker-configuration)
- [Kubernetes Setup](EXAMPLES.md#kubernetes-configuration)
- [Monitoring](EXAMPLES.md#performance-monitoring)
- [Troubleshooting](EXAMPLES.md#troubleshooting-examples)

## 📖 Documentation Structure

```
OElite.Restme.RateLimiting/
├── README.md                           # Main documentation
├── API_DOCUMENTATION.md                # Complete API reference
├── EXAMPLES.md                         # Comprehensive examples
└── ../helios/kortex/
    └── KORTEX_RATELIMITING_MIGRATION_GUIDE.md  # Migration guide
```

## 🎯 Target Audiences

### Developers
- **Quick Start**: [README.md](README.md#quick-start)
- **API Reference**: [API_DOCUMENTATION.md](API_DOCUMENTATION.md)
- **Examples**: [EXAMPLES.md](EXAMPLES.md)

### DevOps Engineers
- **Production Deployment**: [EXAMPLES.md](EXAMPLES.md#production-deployment)
- **Docker/Kubernetes**: [EXAMPLES.md](EXAMPLES.md#docker-configuration)
- **Monitoring**: [EXAMPLES.md](EXAMPLES.md#performance-monitoring)

### Security Teams
- **DDoS Protection**: [README.md](README.md#ddos-protection)
- **Security Best Practices**: [README.md](README.md#security-best-practices)
- **Emergency Mode**: [API_DOCUMENTATION.md](API_DOCUMENTATION.md#emergencymodeconfig)

### Platform Teams
- **Migration Guide**: [KORTEX_RATELIMITING_MIGRATION_GUIDE.md](../helios/kortex/KORTEX_RATELIMITING_MIGRATION_GUIDE.md)
- **Configuration Management**: [EXAMPLES.md](EXAMPLES.md#configuration-management)
- **Performance Optimization**: [README.md](README.md#performance-considerations)

## 🔍 Feature Index

### Core Rate Limiting
- [Fixed Window Algorithm](API_DOCUMENTATION.md#ratelimitalgorithm)
- [Token Bucket Algorithm](API_DOCUMENTATION.md#ratelimitalgorithm)
- [Sliding Window Algorithm](API_DOCUMENTATION.md#ratelimitalgorithm)
- [Leaky Bucket Algorithm](API_DOCUMENTATION.md#ratelimitalgorithm)

### Advanced Security
- [DDoS Protection](README.md#ddos-protection)
- [Progressive Blocking](API_DOCUMENTATION.md#ratelimitoptions)
- [Emergency Mode](API_DOCUMENTATION.md#emergencymodeconfig)
- [IP Blocking](EXAMPLES.md#ddos-protection)

### Enterprise Features
- [Distributed Storage](README.md#distributed-storage)
- [Adaptive Rate Limiting](README.md#adaptive-rate-limiting)
- [Domain-based Limiting](API_DOCUMENTATION.md#ratelimitoptions)
- [Health Checks](EXAMPLES.md#health-check-implementation)

### Storage Options
- [Memory Storage](API_DOCUMENTATION.md#memoryratelimitstore)
- [Redis Storage](API_DOCUMENTATION.md#redisratelimitstore)
- [Custom Storage](EXAMPLES.md#custom-storage-implementation)
- [Hybrid Storage](EXAMPLES.md#hybrid-storage-implementation)

## 📊 Performance Reference

### Algorithm Comparison
| Algorithm | Memory Usage | Accuracy | Burst Support | Best For |
|-----------|--------------|----------|---------------|----------|
| Fixed Window | Low | Good | No | Simple rate limiting |
| Token Bucket | Low | Good | Yes | APIs with bursts |
| Sliding Window | High | Excellent | No | Smooth limiting |
| Leaky Bucket | Low | Good | No | Traffic shaping |

### Storage Performance
| Storage | Latency | Throughput | Scalability | Use Case |
|---------|---------|------------|-------------|----------|
| Memory | ~0.1ms | 100,000+ ops/sec | Single instance | Development |
| Redis | ~1ms | 10,000+ ops/sec | Multi-instance | Production |

## 🛠️ Development Resources

### Code Examples
- [Basic Setup](EXAMPLES.md#basic-setup)
- [Advanced Configuration](EXAMPLES.md#advanced-configuration)
- [Custom Implementations](EXAMPLES.md#custom-storage-implementation)
- [Testing](EXAMPLES.md#testing-examples)

### Configuration Templates
- [Development](EXAMPLES.md#environment-configuration)
- [Production](EXAMPLES.md#environment-configuration)
- [Docker](EXAMPLES.md#docker-configuration)
- [Kubernetes](EXAMPLES.md#kubernetes-configuration)

### Troubleshooting
- [Common Issues](EXAMPLES.md#troubleshooting-examples)
- [Debug Configuration](EXAMPLES.md#debug-configuration)
- [Performance Issues](README.md#performance-considerations)
- [Error Handling](EXAMPLES.md#custom-error-handling)

## 📈 Monitoring and Observability

### Metrics
- `rate_limit_requests_total` - Total requests processed
- `rate_limit_blocks_total` - Total blocked requests
- `rate_limit_ddos_blocks_total` - DDoS blocks
- `rate_limit_emergency_activations_total` - Emergency mode activations

### Logging
- Debug: Rate limit checks
- Warning: Rate limit exceeded
- Error: Rate limiting failures
- Critical: Emergency mode activation

### Health Checks
- Service availability
- Storage connectivity
- Configuration validation
- Performance metrics

## 🔒 Security Considerations

### IP Address Handling
- Direct connections
- Proxy connections
- Load balancer scenarios
- IPv6 support

### Attack Protection
- Rate limiting
- DDoS detection
- Progressive blocking
- Emergency mode
- Adaptive limiting

### Configuration Security
- Environment variables
- Redis connection strings
- Key prefixes
- Monitoring

## 📞 Support and Community

### Documentation Issues
- [GitHub Issues](https://github.com/PhanesDigital/oelite/issues)
- [Documentation Requests](https://github.com/PhanesDigital/oelite/discussions)

### Technical Support
- [GitHub Discussions](https://github.com/PhanesDigital/oelite/discussions)
- [Stack Overflow](https://stackoverflow.com/questions/tagged/oelite)

### Contributing
- [Contributing Guide](https://github.com/PhanesDigital/oelite/blob/main/CONTRIBUTING.md)
- [Code of Conduct](https://github.com/PhanesDigital/oelite/blob/main/CODE_OF_CONDUCT.md)

## 📝 Version History

### v2.0.0 (Current)
- ✅ Advanced DDoS protection
- ✅ Emergency mode
- ✅ Adaptive rate limiting
- ✅ Multiple algorithms
- ✅ Redis distributed storage
- ✅ Comprehensive logging
- ✅ Enhanced monitoring

### v1.0.0
- ✅ Basic rate limiting
- ✅ Memory storage
- ✅ Fixed window algorithm

## 🎉 Getting Help

### Quick Help
1. **Check Examples**: [EXAMPLES.md](EXAMPLES.md)
2. **Review API**: [API_DOCUMENTATION.md](API_DOCUMENTATION.md)
3. **Search Issues**: [GitHub Issues](https://github.com/PhanesDigital/oelite/issues)

### Detailed Help
1. **Read Documentation**: Start with [README.md](README.md)
2. **Follow Examples**: Use [EXAMPLES.md](EXAMPLES.md)
3. **Ask Questions**: [GitHub Discussions](https://github.com/PhanesDigital/oelite/discussions)

---

**Made with ❤️ by the OElite Development Team**

*This documentation is continuously updated. For the latest version, visit our [GitHub repository](https://github.com/PhanesDigital/oelite).*
