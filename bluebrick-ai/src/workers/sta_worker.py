"""Single-threaded-apartment worker for COM-bound tasks."""

from __future__ import annotations

import logging
import queue
import threading
from concurrent.futures import Future
from dataclasses import dataclass
from typing import Any, Callable, Dict, Optional

import pythoncom

LOGGER = logging.getLogger("bluebrick_ai.workers.sta")

ProgressCallback = Callable[[str, Dict[str, Any]], None]
TaskCallable = Callable[[Any, Optional[ProgressCallback]], Any]


@dataclass
class StaTask:
    """Internal representation of a unit of work."""

    func: TaskCallable
    args: tuple
    kwargs: Dict[str, Any]
    future: Future
    progress_callback: Optional[ProgressCallback]


class StaWorker:
    """Dispatches COM calls to a dedicated STA thread."""

    def __init__(
        self,
        setup: Optional[Callable[[], Any]] = None,
        teardown: Optional[Callable[[Any], None]] = None,
        idle_sleep: float = 0.05,
    ) -> None:
        self._setup = setup
        self._teardown = teardown
        self._idle_sleep = idle_sleep
        self._tasks: "queue.Queue[Optional[StaTask]]" = queue.Queue()
        self._thread = threading.Thread(target=self._worker_loop, daemon=True)
        self._resources: Any = None
        self._shutdown_event = threading.Event()
        self._started_event = threading.Event()
        self._thread.start()
        self._started_event.wait()

    def submit(
        self,
        func: TaskCallable,
        *args,
        progress_callback: Optional[ProgressCallback] = None,
        **kwargs,
    ) -> Future:
        """Queue work for execution on the STA thread."""

        future: Future = Future()
        task = StaTask(func=func, args=args, kwargs=kwargs, future=future, progress_callback=progress_callback)
        self._tasks.put(task)
        return future

    def shutdown(self, wait: bool = True) -> None:
        """Stop the worker and release COM resources."""

        self._shutdown_event.set()
        self._tasks.put(None)
        if wait:
            self._thread.join()

    # ------------------------------------------------------------------
    def _worker_loop(self) -> None:
        pythoncom.CoInitialize()
        LOGGER.debug("STA worker thread initialized")
        try:
            if self._setup:
                self._resources = self._setup()
            self._started_event.set()
            while not self._shutdown_event.is_set():
                try:
                    task = self._tasks.get(timeout=self._idle_sleep)
                except queue.Empty:
                    continue
                if task is None:
                    break
                self._execute_task(task)
                self._tasks.task_done()
        finally:
            try:
                if self._teardown and self._resources is not None:
                    self._teardown(self._resources)
            finally:
                pythoncom.CoUninitialize()
                LOGGER.debug("STA worker thread uninitialized")

    def _execute_task(self, task: StaTask) -> None:
        try:
            result = task.func(self._resources, task.progress_callback, *task.args, **task.kwargs)
        except Exception as exc:  # pragma: no cover - error propagation path
            LOGGER.exception("Task execution failed")
            task.future.set_exception(exc)
        else:
            task.future.set_result(result)


def progress_logger(event: str, payload: Dict[str, Any]) -> None:
    """Default progress callback that logs structured updates."""

    LOGGER.info("Progress: %s | %s", event, payload)
