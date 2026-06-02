"""
conftest.py — Shared pytest configuration and fixtures
"""
import pytest


def pytest_addoption(parser):
    """Add --run-live option to enable live integration tests."""
    parser.addoption(
        "--run-live",
        action="store_true",
        default=False,
        help="Run live integration tests that send real emails",
    )


def pytest_collection_modifyitems(config, items):
    """Skip tests marked with @pytest.mark.live unless --run-live is passed."""
    if config.getoption("--run-live"):
        return  # Don't skip live tests

    skip_live = pytest.mark.skip(reason="Need --run-live flag to run live tests")
    for item in items:
        if "live" in item.keywords:
            item.add_marker(skip_live)
