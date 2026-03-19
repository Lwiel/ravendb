import React, { useState } from "react";
import { DistributionItem, DistributionLegend, LocationDistribution } from "components/common/LocationDistribution";
import classNames from "classnames";
import { AnyEtlOngoingTaskInfo, OngoingEtlTaskNodeInfo, OngoingTaskInfo } from "components/models/tasks";
import { ProgressCircle } from "components/common/ProgressCircle";
import { OngoingEtlTaskProgressTooltip } from "../partials/OngoingEtlTaskProgressTooltip";
import { Icon } from "components/common/Icon";
import { databaseLocationComparator } from "components/utils/common";
import { ErrorModal } from "components/pages/database/tasks/ongoingTasks/partials/ErrorModal";
import Badge from "react-bootstrap/Badge";
import Button from "react-bootstrap/Button";
import ButtonWithSpinner from "components/common/ButtonWithSpinner";
import PopoverWithHoverWrapper from "components/common/PopoverWithHoverWrapper";
import { useAppUrls } from "hooks/useAppUrls";
import { databaseSelectors } from "components/common/shell/databaseSliceSelectors";
import { useAppSelector } from "components/store";
import {
    getPopoverMessageForTaskHealth,
    getTaskErrorCount,
    getTaskHealthStatus,
    healthStatusToBadge,
} from "../panels/etlPanelUtils";
import { useServices } from "hooks/useServices";
import { useAsyncCallback } from "react-async-hook";
import EtlTaskStats = Raven.Server.Documents.ETL.Stats.EtlTaskStats;
import EtlErrors = Raven.Server.Documents.ETL.Stats.EtlErrors;

interface OngoingEtlTaskDistributionProps {
    task: AnyEtlOngoingTaskInfo;
    showPreview: (transformationName: string) => void;
    etlStats?: EtlTaskStats[];
    etlErrors?: EtlErrors[];
}

interface ItemWithTooltipProps {
    nodeInfo: OngoingEtlTaskNodeInfo;
    sharded: boolean;
    task: AnyEtlOngoingTaskInfo;
    showPreview: (transformationName: string) => void;
    etlStats?: EtlTaskStats[];
    etlErrors?: EtlErrors[];
}

interface ConnectionStatusCellProps {
    status: Raven.Client.Documents.Operations.OngoingTasks.OngoingTaskConnectionStatus;
    taskName: string;
    nodeTag: string;
    toggleErrorModal: () => void;
}

function ConnectionStatusCell({ status, taskName, nodeTag, toggleErrorModal }: ConnectionStatusCellProps) {
    const { tasksService } = useServices();
    const databaseName = useAppSelector(databaseSelectors.activeDatabaseName);

    const retryBatch = useAsyncCallback(() => tasksService.retryBatch(databaseName, taskName, nodeTag));

    if (status !== "Reconnect") {
        return <span>{status}</span>;
    }

    return (
        <div className="hstack gap-1">
            {status}
            <PopoverWithHoverWrapper
                message={
                    <div className="vstack gap-2 p-1">
                        <div className="d-flex align-items-center gap-1">
                            <Icon icon="clock" margin="m-0" />
                            Next batch retry time in: <b>5 minutes</b>
                        </div>
                        <div className="d-flex gap-2">
                            <ButtonWithSpinner
                                variant="primary"
                                size="sm"
                                className="rounded-pill"
                                icon="refresh"
                                isSpinning={retryBatch.loading}
                                onClick={retryBatch.execute}
                            >
                                Retry now
                            </ButtonWithSpinner>
                            <Button variant="secondary" size="sm" className="rounded-pill" onClick={toggleErrorModal}>
                                <Icon icon="preview" />
                                View error
                            </Button>
                        </div>
                    </div>
                }
            >
                <Icon icon="info" color="info" margin="m-0" />
            </PopoverWithHoverWrapper>
        </div>
    );
}

function ItemWithTooltip(props: ItemWithTooltipProps) {
    const { nodeInfo, sharded, task, showPreview, etlStats, etlErrors } = props;

    const shard = (
        <div className="top shard">
            {nodeInfo.location.shardNumber != null && (
                <>
                    <Icon icon="shard" />
                    {nodeInfo.location.shardNumber}
                </>
            )}
        </div>
    );

    const [errorToDisplay, setErrorToDisplay] = useState<string>(null);

    const toggleErrorModal = () => {
        setErrorToDisplay((error) => (error ? null : nodeInfo.details?.error));
    };

    const key = taskNodeInfoKey(nodeInfo);
    const hasError = !!nodeInfo.details?.error;
    const [node, setNode] = useState<HTMLDivElement>();

    const { appUrl } = useAppUrls();
    const databaseName = useAppSelector(databaseSelectors.activeDatabaseName);

    const taskHealth = getTaskHealthStatus(etlStats ?? [], task.shared.taskName);
    const { bg, icon: heathIcon, label: healthLabel } = healthStatusToBadge(taskHealth);
    const errorCount = getTaskErrorCount(etlErrors ?? [], task.shared.taskName);
    const goToTaskErrors = appUrl.forTasksErrors(databaseName, task.shared.taskName);

    return (
        <div ref={setNode}>
            <DistributionItem loading={nodeInfo.status === "loading" || nodeInfo.status === "idle"} key={key}>
                {sharded && shard}
                <div className={classNames("node", { top: !sharded })}>
                    {!sharded && <Icon icon="node" />}

                    {nodeInfo.location.nodeTag}
                </div>
                <div>
                    {nodeInfo.status === "success" ? (
                        <ConnectionStatusCell
                            status={nodeInfo.details.taskConnectionStatus}
                            taskName={task.shared.taskName}
                            nodeTag={nodeInfo.location.nodeTag}
                            toggleErrorModal={toggleErrorModal}
                        />
                    ) : (
                        ""
                    )}
                </div>
                <div>
                    {hasError || errorCount > 0 ? (
                        <strong>
                            <a
                                href={goToTaskErrors}
                                className="d-flex text-decoration-none text-white align-items-center gap-1 no-decor"
                            >
                                <Icon icon="warning" color="danger" margin="m-0" />
                                {errorCount > 0 && <b>{errorCount}</b>}
                            </a>
                        </strong>
                    ) : (
                        "-"
                    )}
                </div>
                <div className="d-flex align-items-center">
                    <PopoverWithHoverWrapper
                        wrapperClassName="d-flex align-items-center"
                        message={getPopoverMessageForTaskHealth(taskHealth)}
                    >
                        <Badge bg={bg} className="rounded-pill">
                            <Icon icon={heathIcon} />
                            {healthLabel}
                        </Badge>
                    </PopoverWithHoverWrapper>
                </div>
                <OngoingEtlTaskProgress task={task} nodeInfo={nodeInfo} />
            </DistributionItem>
            {node &&
                (errorToDisplay ? (
                    <ErrorModal key="modal" toggleErrorModal={toggleErrorModal} error={errorToDisplay} />
                ) : (
                    <OngoingEtlTaskProgressTooltip
                        hasError={!!nodeInfo.details?.error}
                        toggleErrorModal={toggleErrorModal}
                        target={node}
                        progress={nodeInfo.etlProgress}
                        status={nodeInfo.status}
                        showPreview={showPreview}
                    />
                ))}
        </div>
    );
}

export function OngoingEtlTaskDistribution(props: OngoingEtlTaskDistributionProps) {
    const { task, showPreview, etlStats, etlErrors } = props;
    const sharded = task.nodesInfo.some((x) => x.location.shardNumber != null);

    const visibleNodes = task.nodesInfo.filter(
        (nodeInfo) =>
            nodeInfo.details && task.responsibleLocations.find((l) => databaseLocationComparator(l, nodeInfo.location))
    );

    const items = visibleNodes.map((nodeInfo) => {
        const key = taskNodeInfoKey(nodeInfo);

        return (
            <ItemWithTooltip
                key={key}
                nodeInfo={nodeInfo}
                sharded={sharded}
                showPreview={showPreview}
                task={task}
                etlStats={etlStats}
                etlErrors={etlErrors}
            />
        );
    });

    return (
        <div className="px-3 pb-2">
            <LocationDistribution>
                <DistributionLegend>
                    <div className="top"></div>
                    {sharded && (
                        <div className="node">
                            <Icon icon="node" /> Node
                        </div>
                    )}
                    <div>
                        <Icon icon="connected" /> Connection status
                    </div>
                    <div>
                        <Icon icon="warning" /> Errors
                    </div>
                    <div>
                        <Icon icon="healthcheck" /> Health status
                    </div>
                    <div>
                        <Icon icon="changes" /> State
                    </div>
                </DistributionLegend>
                {items}
            </LocationDistribution>
        </div>
    );
}

interface OngoingEtlTaskProgressProps {
    nodeInfo: OngoingEtlTaskNodeInfo;
    task: OngoingTaskInfo;
}

export function OngoingEtlTaskProgress(props: OngoingEtlTaskProgressProps) {
    const { nodeInfo, task } = props;

    const disabled = task.shared.taskState === "Disabled";

    if (!nodeInfo.etlProgress || nodeInfo.etlProgress.length === 0) {
        return (
            <ProgressCircle icon={disabled ? "stop" : null} state="running">
                {disabled ? "Disabled" : "?"}
            </ProgressCircle>
        );
    }

    if (nodeInfo.etlProgress.every((x) => x.completed) && task.shared.taskState === "Enabled") {
        return (
            <ProgressCircle state="success" icon="check">
                up to date
            </ProgressCircle>
        );
    }

    // at least one transformation is not completed - let's calculate total progress
    const totalItems = nodeInfo.etlProgress.reduce((acc, current) => acc + current.global.total, 0);
    const totalProcessed = nodeInfo.etlProgress.reduce((acc, current) => acc + current.global.processed, 0);

    const percentage = totalItems === 0 ? 1 : Math.floor((totalProcessed * 100) / totalItems) / 100;
    const anyDisabled = nodeInfo.etlProgress.some((x) => x.disabled);

    return (
        <ProgressCircle state="running" icon={anyDisabled ? "stop" : null} progress={percentage}>
            {anyDisabled ? "Disabled" : "Running"}
        </ProgressCircle>
    );
}

const taskNodeInfoKey = (nodeInfo: OngoingEtlTaskNodeInfo) =>
    nodeInfo.location.shardNumber + "__" + nodeInfo.location.nodeTag;
