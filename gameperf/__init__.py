"""Análise local de capturas de tempo de quadro."""

from .analyzer import (
    METRIC_SPECS,
    SOURCE_DISTINCTION,
    analyze,
    compare,
    format_comparison,
)

__all__ = [
    "METRIC_SPECS",
    "SOURCE_DISTINCTION",
    "analyze",
    "compare",
    "format_comparison",
]
